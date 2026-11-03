using EventConnectCore.Models;

namespace EventConnectService.ViewModels;

public enum EventWhen
{
    Upcoming,
    Past
}

/// <summary>Filters for the organizer's event list.</summary>
public record EventQueryViewModel
{
    /// <summary>Only events in this status. Without it, archived events are left out.</summary>
    public EventStatus? Status { get; init; }

    /// <summary>Upcoming or past, by the event's start in UTC. Events without a start match neither.</summary>
    public EventWhen? When { get; init; }

    /// <summary>Title contains this text, ignoring case.</summary>
    public string? Search { get; init; }
}
