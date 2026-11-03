namespace EventConnectService.ViewModels;

public record RegisteredUserViewModel(
    int Id,
    string Email,
    string DisplayName,
    bool EmailVerified);
