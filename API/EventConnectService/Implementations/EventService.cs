using EventConnectCore.Data;
using EventConnectCore.Models;
using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EventConnectService.Implementations;

public class EventService(EventConnectDbContext db) : IEventService
{
    public async Task<IReadOnlyList<EventViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Events
            .AsNoTracking()
            .OrderBy(e => e.StartsAt)
            .Select(e => new EventViewModel(e.Id, e.Title, e.Description, e.Location, e.StartsAt, e.EndsAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<EventViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? null : ToViewModel(entity);
    }

    public async Task<EventViewModel> CreateAsync(CreateEventViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Event
        {
            Title = model.Title,
            Description = model.Description,
            Location = model.Location,
            StartsAt = model.StartsAt,
            EndsAt = model.EndsAt,
            CreatedAt = DateTime.UtcNow
        };

        db.Events.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToViewModel(entity);
    }

    private static EventViewModel ToViewModel(Event e) =>
        new(e.Id, e.Title, e.Description, e.Location, e.StartsAt, e.EndsAt);
}
