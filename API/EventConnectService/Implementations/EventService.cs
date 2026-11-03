using System.Security.Claims;
using EventConnectCore.Data;
using EventConnectCore.Models;
using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventConnectService.Implementations;

public class EventService(EventConnectDbContext db, UserManager<User> userManager) : IEventService
{
    private const int TitleMaxLength = 200;
    private const string CopySuffix = " (copy)";

    public async Task<IReadOnlyList<EventViewModel>> ListAsync(ClaimsPrincipal principal, EventQueryViewModel query, CancellationToken cancellationToken = default)
    {
        if (OrganizerId(principal) is not { } organizerId)
        {
            return [];
        }

        var events = db.Events.AsNoTracking().Where(e => e.OrganizerId == organizerId);
        events = query.Status is { } status
            ? events.Where(e => e.Status == status)
            : events.Where(e => e.Status != EventStatus.Archived);

        var now = DateTime.UtcNow;
        events = query.When switch
        {
            EventWhen.Upcoming => events.Where(e => e.StartsAtUtc >= now),
            EventWhen.Past => events.Where(e => e.StartsAtUtc < now),
            _ => events
        };

        if (query.Search?.Trim() is { Length: > 0 } search)
        {
            var term = search.ToLower();
            events = events.Where(e => e.Title.ToLower().Contains(term));
        }

        var list = await events
            .OrderBy(e => e.StartsAtUtc == null)
            .ThenBy(e => e.StartsAtUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
        return [.. list.Select(ToViewModel)];
    }

    public async Task<EventViewModel?> GetAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnAsync(principal, id, cancellationToken);
        return entity is null ? null : ToViewModel(entity);
    }

    public async Task<ServiceResult<EventViewModel>> CreateAsync(ClaimsPrincipal principal, SaveEventViewModel model, CancellationToken cancellationToken = default)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return ServiceResult.Unauthorized();
        }

        if (model.TimeZone is null)
        {
            model = model with { TimeZone = user.TimeZone };
        }

        var errors = await FormatErrorsAsync(model, currentCategoryId: null, cancellationToken);
        if (errors.Count > 0)
        {
            return ServiceResult.Invalid(errors);
        }

        var now = DateTime.UtcNow;
        var entity = new Event
        {
            OrganizerId = user.Id,
            Status = EventStatus.Draft,
            Title = "",
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(entity, model);

        db.Events.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToViewModel(entity);
    }

    public async Task<ServiceResult<EventViewModel>> UpdateAsync(ClaimsPrincipal principal, int id, UpdateEventViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnAsync(principal, id, cancellationToken, tracked: true);
        if (entity is null)
        {
            return ServiceResult.NotFound();
        }

        if (entity.Status is EventStatus.Cancelled or EventStatus.Archived)
        {
            return ServiceResult.Conflict($"This event is {entity.Status.ToString().ToLowerInvariant()} and can't be edited.");
        }

        var errors = await FormatErrorsAsync(model, entity.CategoryId, cancellationToken);
        if (errors.Count > 0)
        {
            return ServiceResult.Invalid(errors);
        }

        Apply(entity, model);

        if (entity.Status == EventStatus.Published)
        {
            // A published event can still be corrected after it has started, so the start needn't be in the future.
            var publishErrors = PublishErrors(entity, requireFutureStart: false);
            if (publishErrors.Count > 0)
            {
                return ServiceResult.Invalid(publishErrors);
            }

            // Change notices for a material change (model.NotifyGuests, model.ChangeMessage) are queued here once
            // Communication (07) is built. Until then the request is accepted and validated but nothing is sent.
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToViewModel(entity);
    }

    public async Task<ServiceResult<EventViewModel>> PublishAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnAsync(principal, id, cancellationToken, tracked: true);
        if (entity is null)
        {
            return ServiceResult.NotFound();
        }

        if (entity.Status != EventStatus.Draft)
        {
            return ServiceResult.Conflict("Only a draft can be published.");
        }

        var emailVerified = await db.Users.Where(u => u.Id == entity.OrganizerId).Select(u => u.EmailConfirmed).SingleAsync(cancellationToken);
        if (!emailVerified)
        {
            return ServiceResult.Forbidden("Verify your email address before publishing.");
        }

        var errors = PublishErrors(entity, requireFutureStart: true);
        if (errors.Count > 0)
        {
            return ServiceResult.Invalid(errors, "Event is not ready to publish.");
        }

        var now = DateTime.UtcNow;
        entity.Status = EventStatus.Published;
        entity.PublishedAt = now;
        entity.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return ToViewModel(entity);
    }

    public async Task<ServiceResult<EventViewModel>> CancelAsync(ClaimsPrincipal principal, int id, CancelEventViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnAsync(principal, id, cancellationToken, tracked: true);
        if (entity is null)
        {
            return ServiceResult.NotFound();
        }

        if (entity.Status != EventStatus.Published)
        {
            return ServiceResult.Conflict("Only a published event can be cancelled.");
        }

        var now = DateTime.UtcNow;
        entity.Status = EventStatus.Cancelled;
        entity.CancelledAt = now;
        entity.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        // Cancellation notices (with model.Message) and stopping reminders are added by Communication (07);
        // RSVPs are refused for a cancelled event by RSVP (06).
        return ToViewModel(entity);
    }

    public async Task<ServiceResult<EventViewModel>> ArchiveAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnAsync(principal, id, cancellationToken, tracked: true);
        if (entity is null)
        {
            return ServiceResult.NotFound();
        }

        if (entity.Status == EventStatus.Archived)
        {
            return ServiceResult.Conflict("This event is already archived.");
        }

        var now = DateTime.UtcNow;
        entity.Status = EventStatus.Archived;
        entity.ArchivedAt = now;
        entity.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return ToViewModel(entity);
    }

    public async Task<ServiceResult<EventViewModel>> DuplicateAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default)
    {
        var source = await FindOwnAsync(principal, id, cancellationToken);
        if (source is null)
        {
            return ServiceResult.NotFound();
        }

        var now = DateTime.UtcNow;
        var copy = new Event
        {
            OrganizerId = source.OrganizerId,
            Status = EventStatus.Draft,
            Title = CopyTitle(source.Title),
            Description = source.Description,
            CategoryId = source.CategoryId,
            Format = source.Format,
            StartsAt = source.StartsAt,
            StartsAtUtc = source.StartsAtUtc,
            EndsAt = source.EndsAt,
            TimeZone = source.TimeZone,
            VenueName = source.VenueName,
            Address = source.Address,
            VirtualUrl = source.VirtualUrl,
            RsvpDeadline = source.RsvpDeadline,
            RsvpDeadlineUtc = source.RsvpDeadlineUtc,
            Capacity = source.Capacity,
            AllowPlusOnes = source.AllowPlusOnes,
            DefaultMaxPlusOnes = source.DefaultMaxPlusOnes,
            Visibility = source.Visibility,
            CreatedAt = now,
            UpdatedAt = now
        };

        // The design (03) and RSVP questions (06) are copied here once those features exist.
        db.Events.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return ToViewModel(copy);
    }

    public async Task<ServiceResult> DeleteAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnAsync(principal, id, cancellationToken, tracked: true);
        if (entity is null)
        {
            return ServiceResult.NotFound();
        }

        if (entity.Status != EventStatus.Draft)
        {
            return ServiceResult.Conflict("Only a draft can be deleted. Archive this event instead.");
        }

        db.Events.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private int? OrganizerId(ClaimsPrincipal principal) =>
        int.TryParse(userManager.GetUserId(principal), out var id) ? id : null;

    private async Task<Event?> FindOwnAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken, bool tracked = false)
    {
        if (OrganizerId(principal) is not { } organizerId)
        {
            return null;
        }

        var events = tracked ? db.Events : db.Events.AsNoTracking();
        return await events.FirstOrDefaultAsync(e => e.Id == id && e.OrganizerId == organizerId, cancellationToken);
    }

    /// <summary>Rules every save must pass, draft or not: each field on its own, never comparing fields.</summary>
    private async Task<Dictionary<string, string[]>> FormatErrorsAsync(SaveEventViewModel model, int? currentCategoryId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            errors["Title"] = ["Enter a title."];
        }

        foreach (var (field, value) in new[] { ("StartsAt", model.StartsAt), ("EndsAt", model.EndsAt), ("RsvpDeadline", model.RsvpDeadline) })
        {
            if (value is { Kind: not DateTimeKind.Unspecified })
            {
                errors[field] = ["Enter a local date and time without a time zone offset."];
            }
        }

        if (Clean(model.TimeZone) is { } timeZone && !TimeZones.IsIana(timeZone))
        {
            errors["TimeZone"] = ["Unknown time zone. Use an IANA name such as America/New_York."];
        }

        if (Clean(model.VirtualUrl) is { } url
            && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            errors["VirtualUrl"] = ["Enter a full meeting link starting with https://."];
        }

        // An event keeps a category that has since been deactivated; choosing one anew needs an active category.
        if (model.CategoryId is { } categoryId && categoryId != currentCategoryId
            && !await db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive, cancellationToken))
        {
            errors["CategoryId"] = ["Choose a category from the list."];
        }

        return errors;
    }

    /// <summary>Everything a published event needs; every problem is reported, not just the first.</summary>
    private static Dictionary<string, string[]> PublishErrors(Event entity, bool requireFutureStart)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(entity.Title))
        {
            errors["Title"] = ["Enter a title."];
        }

        if (entity.CategoryId is null)
        {
            errors["CategoryId"] = ["Choose a category."];
        }

        if (entity.StartsAt is null)
        {
            errors["StartsAt"] = ["Choose when the event starts."];
        }
        else if (requireFutureStart && entity.StartsAtUtc <= DateTime.UtcNow)
        {
            errors["StartsAt"] = ["The start must be in the future."];
        }

        if (entity.TimeZone is null)
        {
            errors["TimeZone"] = ["Choose a time zone."];
        }

        if (entity.Format is null)
        {
            errors["Format"] = ["Choose whether the event is in person, virtual or hybrid."];
        }

        if (entity.Format is EventFormat.Physical or EventFormat.Hybrid)
        {
            if (entity.VenueName is null)
            {
                errors["VenueName"] = ["A venue name is required for in-person events."];
            }

            if (entity.Address is null)
            {
                errors["Address"] = ["An address is required for in-person events."];
            }
        }

        if (entity.Format is EventFormat.Virtual or EventFormat.Hybrid && entity.VirtualUrl is null)
        {
            errors["VirtualUrl"] = ["A meeting link is required for virtual events."];
        }

        if (entity.StartsAt is { } start)
        {
            if (entity.EndsAt <= start)
            {
                errors["EndsAt"] = ["End must be after start."];
            }

            if (entity.RsvpDeadline >= start)
            {
                errors["RsvpDeadline"] = ["The RSVP deadline must be before the start."];
            }
        }

        if (entity.Capacity < 1)
        {
            errors["Capacity"] = ["Capacity must be at least 1."];
        }

        return errors;
    }

    /// <summary>Copies the editable fields onto the entity and recomputes the UTC times.</summary>
    private static void Apply(Event entity, SaveEventViewModel model)
    {
        entity.Title = model.Title.Trim();
        entity.Description = Clean(model.Description);
        entity.CategoryId = model.CategoryId;
        entity.Format = model.Format;
        entity.TimeZone = Clean(model.TimeZone);
        entity.VenueName = Clean(model.VenueName);
        entity.Address = Clean(model.Address);
        entity.VirtualUrl = Clean(model.VirtualUrl);
        entity.Capacity = model.Capacity;
        entity.AllowPlusOnes = model.AllowPlusOnes;
        entity.DefaultMaxPlusOnes = model.DefaultMaxPlusOnes;
        entity.Visibility = model.Visibility;

        (entity.StartsAt, entity.StartsAtUtc) = Resolve(model.StartsAt, entity.TimeZone);
        (entity.EndsAt, _) = Resolve(model.EndsAt, entity.TimeZone);
        (entity.RsvpDeadline, entity.RsvpDeadlineUtc) = Resolve(model.RsvpDeadline, entity.TimeZone);
    }

    /// <summary>
    /// The local time as stored (moved forward if it fell in a DST gap) and its UTC instant. Without a time zone
    /// the local time is kept as entered and has no UTC instant yet.
    /// </summary>
    private static (DateTime? Local, DateTime? Utc) Resolve(DateTime? local, string? timeZone)
    {
        if (local is not { } value)
        {
            return (null, null);
        }

        if (timeZone is null)
        {
            return (DateTime.SpecifyKind(value, DateTimeKind.Unspecified), null);
        }

        var utc = TimeZones.ToUtc(value, timeZone);
        return (TimeZones.ToLocal(utc, timeZone), utc);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CopyTitle(string title) =>
        title.Length + CopySuffix.Length <= TitleMaxLength
            ? title + CopySuffix
            : title[..(TitleMaxLength - CopySuffix.Length)].TrimEnd() + CopySuffix;

    private static EventViewModel ToViewModel(Event e) => new(
        e.Id,
        e.Status,
        e.Title,
        e.Description,
        e.CategoryId,
        e.Format,
        Local(e.StartsAt),
        Local(e.EndsAt),
        e.TimeZone,
        e.VenueName,
        e.Address,
        e.VirtualUrl,
        Local(e.RsvpDeadline),
        e.Capacity,
        e.AllowPlusOnes,
        e.DefaultMaxPlusOnes,
        e.Visibility,
        Utc(e.PublishedAt),
        Utc(e.CancelledAt),
        Utc(e.ArchivedAt),
        Utc(e.CreatedAt),
        Utc(e.UpdatedAt));

    // The database returns unspecified kinds; mark them so JSON writes UTC times with "Z" and local ones without.
    private static DateTime? Local(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : null;
    private static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
