using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

/// <summary>The body of an edit: every editable field, plus whether to tell guests about a material change.</summary>
public record UpdateEventViewModel : SaveEventViewModel
{
    /// <summary>Queue change notices when a published event's date, time or location changes (07).</summary>
    public bool NotifyGuests { get; init; }

    [MaxLength(1000)]
    public string? ChangeMessage { get; init; }
}
