using System.ComponentModel.DataAnnotations;
using EventConnectCore.Models;

namespace EventConnectService.ViewModels;

/// <summary>
/// The editable event fields: the body of create (only the title is required) and edit (omitted fields are cleared).
/// Date-times are local to <see cref="TimeZone"/> and must not carry an offset.
/// </summary>
public record SaveEventViewModel
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = "";

    [MaxLength(5000)]
    public string? Description { get; init; }

    public int? CategoryId { get; init; }
    public EventFormat? Format { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }

    [MaxLength(64)]
    public string? TimeZone { get; init; }

    [MaxLength(200)]
    public string? VenueName { get; init; }

    [MaxLength(500)]
    public string? Address { get; init; }

    [MaxLength(2000)]
    public string? VirtualUrl { get; init; }

    public DateTime? RsvpDeadline { get; init; }

    [Range(1, int.MaxValue)]
    public int? Capacity { get; init; }

    public bool AllowPlusOnes { get; init; }

    [Range(0, 10)]
    public int DefaultMaxPlusOnes { get; init; } = 1;

    public EventVisibility Visibility { get; init; }
}
