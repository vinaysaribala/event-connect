using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record UpdateProfileViewModel(
    [Required, MaxLength(100)] string DisplayName,
    [Required, MaxLength(64)] string TimeZone);
