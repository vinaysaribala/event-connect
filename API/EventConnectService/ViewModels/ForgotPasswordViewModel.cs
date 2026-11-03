using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record ForgotPasswordViewModel([Required, EmailAddress] string Email);
