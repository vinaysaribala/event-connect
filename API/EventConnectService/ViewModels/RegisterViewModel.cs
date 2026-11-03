using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record RegisterViewModel(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(10)] string Password,
    [Required, MaxLength(100)] string DisplayName);
