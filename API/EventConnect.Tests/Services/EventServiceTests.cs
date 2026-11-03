using System.Security.Claims;
using EventConnect.Tests.Support;
using EventConnectCore.Models;
using EventConnectService;
using EventConnectService.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EventConnect.Tests.Services;

/// <summary>The rules in docs/requirements/02-events.md, at the service layer.</summary>
public sealed class EventServiceTests : IAsyncLifetime
{
    private const int BirthdayCategoryId = 1;
    private static readonly DateTime FutureStart = new(2030, 12, 5, 19, 0, 0);

    private readonly ServiceTestHost host = new();
    private ClaimsPrincipal organizer = null!;
    private int organizerId;

    public async ValueTask InitializeAsync()
    {
        organizerId = await host.RegisterAsync("maria@example.com");
        await host.VerifyEmailAsync(organizerId);
        organizer = ServiceTestHost.PrincipalFor(organizerId);
    }

    public ValueTask DisposeAsync() => host.DisposeAsync();

    // Create (drafts)

    [Fact]
    public async Task Create_with_only_a_title_saves_a_draft_in_the_profile_time_zone()
    {
        await host.ProfileAsync(p => p.UpdateAsync(organizer, new("Maria", "America/Chicago")));

        var result = await host.EventsAsync(e => e.CreateAsync(organizer, new() { Title = "  Maya's 30th  " }));

        Assert.True(result.Succeeded);
        var created = result.Value!;
        Assert.Equal((EventStatus.Draft, "Maya's 30th", "America/Chicago"), (created.Status, created.Title, created.TimeZone));
        Assert.Equal((false, 1, EventVisibility.Private), (created.AllowPlusOnes, created.DefaultMaxPlusOnes, created.Visibility));
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
    }

    [Fact]
    public async Task A_draft_may_have_an_end_before_its_start_and_no_location()
    {
        var result = await host.EventsAsync(e => e.CreateAsync(organizer, new()
        {
            Title = "Half done",
            Format = EventFormat.Physical,
            StartsAt = FutureStart,
            EndsAt = FutureStart.AddHours(-2),
            RsvpDeadline = FutureStart.AddDays(1)
        }));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("TimeZone")]
    [InlineData("VirtualUrl")]
    [InlineData("CategoryId")]
    [InlineData("StartsAt")]
    [InlineData("Title")]
    public async Task Create_rejects_a_field_that_fails_its_format_rule(string field)
    {
        var model = field switch
        {
            "TimeZone" => new SaveEventViewModel { Title = "Party", TimeZone = "Eastern Standard Time" },
            "VirtualUrl" => new SaveEventViewModel { Title = "Party", VirtualUrl = "http://meet.example.com/abc" },
            "CategoryId" => new SaveEventViewModel { Title = "Party", CategoryId = 999 },
            "StartsAt" => new SaveEventViewModel { Title = "Party", StartsAt = DateTime.SpecifyKind(FutureStart, DateTimeKind.Utc) },
            _ => new SaveEventViewModel { Title = "   " }
        };

        var result = await host.EventsAsync(e => e.CreateAsync(organizer, model));

        Assert.Equal(ServiceResultStatus.Invalid, result.Outcome.Status);
        Assert.Equal([field], result.Outcome.Errors.Keys);
    }

    [Fact]
    public async Task An_inactive_category_cannot_be_chosen_but_an_event_already_using_it_keeps_it()
    {
        var created = await CreateReadyAsync();
        await host.DbAsync(db => db.Categories.Where(c => c.Id == BirthdayCategoryId).ExecuteUpdateAsync(c => c.SetProperty(x => x.IsActive, false)));

        var kept = await host.EventsAsync(e => e.UpdateAsync(organizer, created.Id, EditOf(created) with { Title = "Renamed" }));
        var chosenAnew = await host.EventsAsync(e => e.CreateAsync(organizer, new() { Title = "New", CategoryId = BirthdayCategoryId }));

        Assert.True(kept.Succeeded);
        Assert.Equal(["CategoryId"], chosenAnew.Outcome.Errors.Keys);
    }

    // Publish

    [Fact]
    public async Task Publishing_a_title_only_draft_lists_every_missing_field() // AC-01
    {
        var draft = (await host.EventsAsync(e => e.CreateAsync(organizer, new() { Title = "Party", TimeZone = null }))).Value!;
        // The profile zone (UTC) was filled in; clear it to see the time zone rule too.
        await host.EventsAsync(e => e.UpdateAsync(organizer, draft.Id, new() { Title = "Party", TimeZone = "" }));

        var result = await host.EventsAsync(e => e.PublishAsync(organizer, draft.Id));

        Assert.Equal(ServiceResultStatus.Invalid, result.Outcome.Status);
        Assert.Equal("Event is not ready to publish.", result.Outcome.Message);
        Assert.Equal(["CategoryId", "Format", "StartsAt", "TimeZone"], result.Outcome.Errors.Keys.Order());
    }

    [Theory]
    [InlineData(EventFormat.Physical, new[] { "Address", "VenueName" })]
    [InlineData(EventFormat.Virtual, new[] { "VirtualUrl" })]
    [InlineData(EventFormat.Hybrid, new[] { "Address", "VenueName", "VirtualUrl" })]
    public async Task Each_format_needs_its_location_fields_to_publish(EventFormat format, string[] missing)
    {
        var draft = await CreateAsync(Ready() with { Format = format, VenueName = null, Address = null, VirtualUrl = null });

        var result = await host.EventsAsync(e => e.PublishAsync(organizer, draft.Id));

        Assert.Equal(missing, result.Outcome.Errors.Keys.Order());
    }

    [Fact]
    public async Task Publishing_checks_end_deadline_and_a_future_start_together()
    {
        var past = new DateTime(2020, 1, 10, 19, 0, 0);
        var draft = await CreateAsync(Ready() with { StartsAt = past, EndsAt = past.AddHours(-1), RsvpDeadline = past.AddDays(1) });

        var result = await host.EventsAsync(e => e.PublishAsync(organizer, draft.Id));

        Assert.Equal(["EndsAt", "RsvpDeadline", "StartsAt"], result.Outcome.Errors.Keys.Order());
    }

    [Fact]
    public async Task Publishing_a_valid_draft_sets_published_and_the_time()
    {
        var draft = await CreateAsync(Ready());

        var result = await host.EventsAsync(e => e.PublishAsync(organizer, draft.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(EventStatus.Published, result.Value!.Status);
        Assert.NotNull(result.Value.PublishedAt);
    }

    [Fact]
    public async Task An_unverified_organizer_cannot_publish()
    {
        var unverified = ServiceTestHost.PrincipalFor(await host.RegisterAsync("new@example.com"));
        var draft = (await host.EventsAsync(e => e.CreateAsync(unverified, Ready()))).Value!;

        var result = await host.EventsAsync(e => e.PublishAsync(unverified, draft.Id));

        Assert.Equal(ServiceResultStatus.Forbidden, result.Outcome.Status);
    }

    [Fact]
    public async Task Only_a_draft_can_be_published()
    {
        var published = await CreatePublishedAsync();

        var result = await host.EventsAsync(e => e.PublishAsync(organizer, published.Id));

        Assert.Equal(ServiceResultStatus.Conflict, result.Outcome.Status);
    }

    // Edit

    [Fact]
    public async Task A_published_event_must_still_pass_the_publish_rules_when_edited()
    {
        var published = await CreatePublishedAsync();

        var result = await host.EventsAsync(e => e.UpdateAsync(organizer, published.Id, EditOf(published) with { Address = null }));

        Assert.Equal(["Address"], result.Outcome.Errors.Keys);
        Assert.Equal("123 Main St", (await host.EventsAsync(e => e.GetAsync(organizer, published.Id)))!.Address);
    }

    [Fact]
    public async Task A_published_event_can_be_moved_into_the_past_and_notify_guests_is_accepted()
    {
        var published = await CreatePublishedAsync();
        var past = new DateTime(2020, 1, 10, 19, 0, 0);

        var result = await host.EventsAsync(e => e.UpdateAsync(organizer, published.Id,
            EditOf(published) with { StartsAt = past, EndsAt = past.AddHours(2), RsvpDeadline = null, NotifyGuests = true, ChangeMessage = "Sorry, wrong year" }));

        Assert.True(result.Succeeded);
        Assert.Equal(past, result.Value!.StartsAt);
    }

    [Fact]
    public async Task Editing_replaces_every_field_so_omitted_ones_are_cleared()
    {
        var draft = await CreateAsync(Ready());

        var result = await host.EventsAsync(e => e.UpdateAsync(organizer, draft.Id, new() { Title = "Just a title" }));

        Assert.Equal(("Just a title", null, null, null), (result.Value!.Title, result.Value.CategoryId, result.Value.VenueName, result.Value.TimeZone));
    }

    [Theory]
    [InlineData(EventStatus.Cancelled)]
    [InlineData(EventStatus.Archived)]
    public async Task Cancelled_and_archived_events_are_read_only(EventStatus status)
    {
        var published = await CreatePublishedAsync();
        await (status == EventStatus.Cancelled
            ? host.EventsAsync(e => e.CancelAsync(organizer, published.Id, new(null)))
            : host.EventsAsync(e => e.ArchiveAsync(organizer, published.Id)));

        var result = await host.EventsAsync(e => e.UpdateAsync(organizer, published.Id, EditOf(published)));

        Assert.Equal(ServiceResultStatus.Conflict, result.Outcome.Status);
    }

    // Cancel, archive, delete

    [Fact]
    public async Task Cancelling_a_published_event_cannot_be_undone() // AC-07, event part
    {
        var published = await CreatePublishedAsync();

        var cancelled = await host.EventsAsync(e => e.CancelAsync(organizer, published.Id, new("Storm")));
        var republish = await host.EventsAsync(e => e.PublishAsync(organizer, published.Id));
        var cancelAgain = await host.EventsAsync(e => e.CancelAsync(organizer, published.Id, new(null)));

        Assert.Equal(EventStatus.Cancelled, cancelled.Value!.Status);
        Assert.NotNull(cancelled.Value.CancelledAt);
        Assert.Equal(ServiceResultStatus.Conflict, republish.Outcome.Status);
        Assert.Equal(ServiceResultStatus.Conflict, cancelAgain.Outcome.Status);
    }

    [Fact]
    public async Task A_draft_cannot_be_cancelled()
    {
        var draft = await CreateAsync(Ready());

        Assert.Equal(ServiceResultStatus.Conflict, (await host.EventsAsync(e => e.CancelAsync(organizer, draft.Id, new(null)))).Outcome.Status);
    }

    [Fact]
    public async Task Any_status_can_be_archived_once()
    {
        var draft = await CreateAsync(Ready());

        var archived = await host.EventsAsync(e => e.ArchiveAsync(organizer, draft.Id));
        var again = await host.EventsAsync(e => e.ArchiveAsync(organizer, draft.Id));

        Assert.Equal(EventStatus.Archived, archived.Value!.Status);
        Assert.NotNull(archived.Value.ArchivedAt);
        Assert.Equal(ServiceResultStatus.Conflict, again.Outcome.Status);
    }

    [Fact]
    public async Task Only_a_draft_can_be_deleted()
    {
        var draft = await CreateAsync(Ready());
        var published = await CreatePublishedAsync();

        Assert.True((await host.EventsAsync(e => e.DeleteAsync(organizer, draft.Id))).Succeeded);
        Assert.Null(await host.EventsAsync(e => e.GetAsync(organizer, draft.Id)));
        Assert.Equal(ServiceResultStatus.Conflict, (await host.EventsAsync(e => e.DeleteAsync(organizer, published.Id))).Status);
    }

    // Duplicate

    [Fact]
    public async Task Duplicating_copies_details_into_a_new_draft()
    {
        var published = await CreatePublishedAsync();

        var copy = (await host.EventsAsync(e => e.DuplicateAsync(organizer, published.Id))).Value!;

        Assert.NotEqual(published.Id, copy.Id);
        Assert.Equal((EventStatus.Draft, "Maya's 30th (copy)"), (copy.Status, copy.Title));
        Assert.Null(copy.PublishedAt);
        Assert.Equal(
            published with { Id = 0, Status = EventStatus.Draft, Title = "", PublishedAt = null, CreatedAt = default, UpdatedAt = default },
            copy with { Id = 0, Title = "", CreatedAt = default, UpdatedAt = default });
    }

    [Fact]
    public async Task A_duplicated_title_stays_within_200_characters()
    {
        var draft = await CreateAsync(new() { Title = new string('a', 200) });

        var copy = (await host.EventsAsync(e => e.DuplicateAsync(organizer, draft.Id))).Value!;

        Assert.Equal(200, copy.Title.Length);
        Assert.EndsWith(" (copy)", copy.Title);
    }

    // List

    [Fact]
    public async Task List_sorts_by_UTC_start_with_undated_drafts_last_and_leaves_out_archived()
    {
        // 19:00 in Tokyo is 10:00 UTC; 19:00 in New York is 00:00 UTC the next day.
        var newYork = await CreateAsync(new() { Title = "New York", StartsAt = FutureStart, TimeZone = "America/New_York" });
        var undated = await CreateAsync(new() { Title = "Undated" });
        var tokyo = await CreateAsync(new() { Title = "Tokyo", StartsAt = FutureStart, TimeZone = "Asia/Tokyo" });
        var archived = await CreateAsync(new() { Title = "Archived", StartsAt = FutureStart });
        await host.EventsAsync(e => e.ArchiveAsync(organizer, archived.Id));

        var all = await host.EventsAsync(e => e.ListAsync(organizer, new()));
        var onlyArchived = await host.EventsAsync(e => e.ListAsync(organizer, new() { Status = EventStatus.Archived }));

        Assert.Equal([tokyo.Id, newYork.Id, undated.Id], all.Select(e => e.Id));
        Assert.Equal([archived.Id], onlyArchived.Select(e => e.Id));
    }

    [Fact]
    public async Task List_filters_upcoming_past_and_title_search()
    {
        var upcoming = await CreateAsync(new() { Title = "Rooftop Party", StartsAt = FutureStart });
        var past = await CreateAsync(new() { Title = "Old dinner", StartsAt = new DateTime(2020, 1, 1, 19, 0, 0) });
        await CreateAsync(new() { Title = "Undated party" });

        Assert.Equal([upcoming.Id], (await host.EventsAsync(e => e.ListAsync(organizer, new() { When = EventWhen.Upcoming }))).Select(e => e.Id));
        Assert.Equal([past.Id], (await host.EventsAsync(e => e.ListAsync(organizer, new() { When = EventWhen.Past }))).Select(e => e.Id));
        Assert.Equal(2, (await host.EventsAsync(e => e.ListAsync(organizer, new() { Search = " PARTY " }))).Count);
    }

    // Ownership (FR-004)

    [Fact]
    public async Task Another_organizers_event_is_not_found_by_every_operation()
    {
        var mine = await CreateAsync(Ready());
        var other = ServiceTestHost.PrincipalFor(await host.RegisterAsync("other@example.com"));

        Assert.Empty(await host.EventsAsync(e => e.ListAsync(other, new())));
        Assert.Null(await host.EventsAsync(e => e.GetAsync(other, mine.Id)));
        Assert.All(
            [
                (await host.EventsAsync(e => e.UpdateAsync(other, mine.Id, new() { Title = "Mine now" }))).Outcome,
                (await host.EventsAsync(e => e.PublishAsync(other, mine.Id))).Outcome,
                (await host.EventsAsync(e => e.CancelAsync(other, mine.Id, new(null)))).Outcome,
                (await host.EventsAsync(e => e.ArchiveAsync(other, mine.Id))).Outcome,
                (await host.EventsAsync(e => e.DuplicateAsync(other, mine.Id))).Outcome,
                await host.EventsAsync(e => e.DeleteAsync(other, mine.Id))
            ],
            outcome => Assert.Equal(ServiceResultStatus.NotFound, outcome.Status));
    }

    // Time zones (BR-02)

    [Fact]
    public async Task UTC_copies_follow_the_event_time_zone()
    {
        var created = await CreateAsync(new()
        {
            Title = "Party",
            TimeZone = "America/New_York",
            StartsAt = new DateTime(2030, 7, 1, 19, 0, 0),
            RsvpDeadline = new DateTime(2030, 6, 28, 23, 59, 0)
        });

        var stored = await StoredAsync(created.Id);

        Assert.Equal(new DateTime(2030, 7, 1, 23, 0, 0), stored.StartsAtUtc);
        Assert.Equal(new DateTime(2030, 6, 29, 3, 59, 0), stored.RsvpDeadlineUtc);
    }

    [Fact]
    public async Task A_start_in_a_DST_gap_moves_forward_by_the_gap()
    {
        // US clocks go from 02:00 to 03:00 on 14 March 2027, so 02:30 doesn't exist.
        var created = await CreateAsync(new() { Title = "Party", TimeZone = "America/New_York", StartsAt = new DateTime(2027, 3, 14, 2, 30, 0) });

        Assert.Equal(new DateTime(2027, 3, 14, 3, 30, 0), created.StartsAt);
        Assert.Equal(new DateTime(2027, 3, 14, 7, 30, 0), (await StoredAsync(created.Id)).StartsAtUtc);
    }

    [Fact]
    public async Task A_start_in_a_DST_overlap_uses_the_earlier_instant()
    {
        // US clocks go from 02:00 back to 01:00 on 7 November 2027, so 01:30 happens twice; the first is 01:30 EDT.
        var created = await CreateAsync(new() { Title = "Party", TimeZone = "America/New_York", StartsAt = new DateTime(2027, 11, 7, 1, 30, 0) });

        Assert.Equal(new DateTime(2027, 11, 7, 5, 30, 0), (await StoredAsync(created.Id)).StartsAtUtc);
    }

    // Categories

    [Fact]
    public async Task Categories_lists_the_seeded_active_ones_in_order()
    {
        await host.DbAsync(db => db.Categories.Where(c => c.Slug == "holiday").ExecuteUpdateAsync(c => c.SetProperty(x => x.IsActive, false)));

        var categories = await host.CategoriesAsync(c => c.GetActiveAsync());

        Assert.Equal(["Birthday", "Wedding", "Baby Shower", "Dinner Party", "Business", "Other"], categories.Select(c => c.Name));
    }

    /// <summary>Every field a Physical event needs to publish.</summary>
    private static UpdateEventViewModel Ready() => new()
    {
        Title = "Maya's 30th",
        Description = "Dinner and drinks on the rooftop.",
        CategoryId = BirthdayCategoryId,
        Format = EventFormat.Physical,
        StartsAt = FutureStart,
        EndsAt = FutureStart.AddHours(4),
        TimeZone = "America/New_York",
        VenueName = "Skyline Rooftop",
        Address = "123 Main St",
        RsvpDeadline = FutureStart.AddDays(-7),
        Capacity = 40,
        AllowPlusOnes = true,
        DefaultMaxPlusOnes = 2,
        Visibility = EventVisibility.Public
    };

    private static UpdateEventViewModel EditOf(EventViewModel e) => new()
    {
        Title = e.Title,
        Description = e.Description,
        CategoryId = e.CategoryId,
        Format = e.Format,
        StartsAt = e.StartsAt,
        EndsAt = e.EndsAt,
        TimeZone = e.TimeZone,
        VenueName = e.VenueName,
        Address = e.Address,
        VirtualUrl = e.VirtualUrl,
        RsvpDeadline = e.RsvpDeadline,
        Capacity = e.Capacity,
        AllowPlusOnes = e.AllowPlusOnes,
        DefaultMaxPlusOnes = e.DefaultMaxPlusOnes,
        Visibility = e.Visibility
    };

    private async Task<EventViewModel> CreateAsync(SaveEventViewModel model)
    {
        var result = await host.EventsAsync(e => e.CreateAsync(organizer, model));
        return result.Value ?? throw new InvalidOperationException($"Create failed: {string.Join(", ", result.Outcome.Errors.Keys)}");
    }

    private async Task<EventViewModel> CreateReadyAsync() => await CreateAsync(Ready());

    private async Task<EventViewModel> CreatePublishedAsync()
    {
        var draft = await CreateReadyAsync();
        return (await host.EventsAsync(e => e.PublishAsync(organizer, draft.Id))).Value!;
    }

    private Task<Event> StoredAsync(int id) => host.DbAsync(db => db.Events.AsNoTracking().SingleAsync(e => e.Id == id));
}
