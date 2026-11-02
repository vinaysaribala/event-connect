using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record CreateEventViewModel(
    [Required, MaxLength(200)] string Title,
    string? Description,
    [MaxLength(300)] string? Location,
    DateTime StartsAt,
    DateTime? EndsAt);
