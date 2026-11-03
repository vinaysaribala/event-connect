namespace EventConnectCore.Models;

public class Event
{
    public int Id { get; set; }
    public int OrganizerId { get; set; }
    public User? Organizer { get; set; }
    public EventStatus Status { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public EventFormat? Format { get; set; }

    /// <summary>Local date-time in <see cref="TimeZone"/>.</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary><see cref="StartsAt"/> in UTC, recomputed on every save; for filtering and sorting.</summary>
    public DateTime? StartsAtUtc { get; set; }

    /// <summary>Local date-time in <see cref="TimeZone"/>.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>IANA time zone id, e.g. America/New_York.</summary>
    public string? TimeZone { get; set; }

    public string? VenueName { get; set; }
    public string? Address { get; set; }
    public string? VirtualUrl { get; set; }

    /// <summary>Local date-time in <see cref="TimeZone"/>.</summary>
    public DateTime? RsvpDeadline { get; set; }

    /// <summary><see cref="RsvpDeadline"/> in UTC, recomputed on every save; for deadline checks (BR-02).</summary>
    public DateTime? RsvpDeadlineUtc { get; set; }

    /// <summary>Maximum attendees including plus-ones; null means unlimited.</summary>
    public int? Capacity { get; set; }

    public bool AllowPlusOnes { get; set; }
    public int DefaultMaxPlusOnes { get; set; } = 1;
    public EventVisibility Visibility { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
