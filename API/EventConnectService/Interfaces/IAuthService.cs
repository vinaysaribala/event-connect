using System.Security.Claims;
using EventConnectService.ViewModels;

namespace EventConnectService.Interfaces;

public interface IAuthService
{
    /// <summary>Creates the account, sends the verification email and signs the new user in.</summary>
    Task<ServiceResult<RegisteredUserViewModel>> RegisterAsync(RegisterViewModel model);
    Task<ServiceResult> VerifyEmailAsync(VerifyEmailViewModel model);
    Task ResendVerificationAsync(ClaimsPrincipal principal);

    /// <summary>Signs in and returns the user, or null when the email or password is wrong.</summary>
    Task<UserViewModel?> LoginAsync(LoginViewModel model);
    Task LogoutAsync();
    Task ForgotPasswordAsync(ForgotPasswordViewModel model);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model);
}
