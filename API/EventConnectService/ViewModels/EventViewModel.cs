using EventConnectCore.Models;

namespace EventConnectService.ViewModels;

/// <summary>
/// An event as the organizer sees it. Local date-times (start, end, deadline) are in <see cref="TimeZone"/> and
/// have no offset; the other timestamps are UTC.
/// </summary>
public record EventViewModel(
    int Id,
    EventStatus Status,
    string Title,
    string? Description,
    int? CategoryId,
    EventFormat? Format,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? TimeZone,
    string? VenueName,
    string? Address,
    string? VirtualUrl,
    DateTime? RsvpDeadline,
    int? Capacity,
    bool AllowPlusOnes,
    int DefaultMaxPlusOnes,
    EventVisibility Visibility,
    DateTime? PublishedAt,
    DateTime? CancelledAt,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
