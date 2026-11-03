using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record VerifyEmailViewModel([Required] string Token);
