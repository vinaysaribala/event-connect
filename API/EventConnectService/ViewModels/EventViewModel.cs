namespace EventConnectService.ViewModels;

public record EventViewModel(
    int Id,
    string Title,
    string? Description,
    string? Location,
    DateTime StartsAt,
    DateTime? EndsAt);
