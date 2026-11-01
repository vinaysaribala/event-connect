using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record CreateEventViewModel(
    [property: Required, MaxLength(200)] string Title,
    string? Description,
    [property: MaxLength(300)] string? Location,
    DateTime StartsAt,
    DateTime? EndsAt);
