using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record LoginViewModel(
    [Required] string Email,
    [Required] string Password);
