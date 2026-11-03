namespace EventConnectService.ViewModels;

public record UserViewModel(
    int Id,
    string Email,
    string DisplayName,
    bool EmailVerified,
    IReadOnlyList<string> Roles);
