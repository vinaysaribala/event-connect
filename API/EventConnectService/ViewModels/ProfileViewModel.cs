namespace EventConnectService.ViewModels;

public record ProfileViewModel(
    int Id,
    string Email,
    string DisplayName,
    bool EmailVerified,
    IReadOnlyList<string> Roles,
    string TimeZone);
