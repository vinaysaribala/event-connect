using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record ResetPasswordViewModel(
    [Required] string Token,
    [Required, MinLength(10)] string NewPassword);
