using EventConnectService.ViewModels;

namespace EventConnectService.Interfaces;

public interface IEventService
{
    Task<IReadOnlyList<EventViewModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EventViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EventViewModel> CreateAsync(CreateEventViewModel model, CancellationToken cancellationToken = default);
}
