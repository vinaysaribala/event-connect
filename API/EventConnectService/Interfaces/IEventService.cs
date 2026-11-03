using System.Security.Claims;
using EventConnectService.ViewModels;

namespace EventConnectService.Interfaces;

/// <summary>
/// The signed-in organizer's events. Another organizer's event is reported as not found (FR-004).
/// </summary>
public interface IEventService
{
    Task<IReadOnlyList<EventViewModel>> ListAsync(ClaimsPrincipal principal, EventQueryViewModel query, CancellationToken cancellationToken = default);
    Task<EventViewModel?> GetAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a draft. The time zone defaults to the organizer's profile zone.</summary>
    Task<ServiceResult<EventViewModel>> CreateAsync(ClaimsPrincipal principal, SaveEventViewModel model, CancellationToken cancellationToken = default);

    /// <summary>Replaces every editable field. A published event must still pass the publish rules (except a future start).</summary>
    Task<ServiceResult<EventViewModel>> UpdateAsync(ClaimsPrincipal principal, int id, UpdateEventViewModel model, CancellationToken cancellationToken = default);

    Task<ServiceResult<EventViewModel>> PublishAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default);
    Task<ServiceResult<EventViewModel>> CancelAsync(ClaimsPrincipal principal, int id, CancelEventViewModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult<EventViewModel>> ArchiveAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default);

    /// <summary>Copies details and RSVP settings into a new draft titled "&lt;title&gt; (copy)"; guests are not copied.</summary>
    Task<ServiceResult<EventViewModel>> DuplicateAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a draft.</summary>
    Task<ServiceResult> DeleteAsync(ClaimsPrincipal principal, int id, CancellationToken cancellationToken = default);
}
