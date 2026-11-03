using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisteredUserViewModel>> Register(RegisterViewModel model)
    {
        var result = await authService.RegisterAsync(model);
        return result.Succeeded ? Created("/api/me", result.Value) : this.Failure(result.Outcome);
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        var result = await authService.VerifyEmailAsync(model);
        return result.Succeeded ? NoContent() : this.Failure(result);
    }

    [Authorize]
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> ResendVerification()
    {
        await authService.ResendVerificationAsync(User);
        return Accepted();
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserViewModel>> Login(LoginViewModel model)
    {
        var user = await authService.LoginAsync(model);
        return user is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email or password is incorrect")
            : Ok(user);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout()
    {
        await authService.LogoutAsync();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        await authService.ForgotPasswordAsync(model);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var result = await authService.ResetPasswordAsync(model);
        return result.Succeeded ? NoContent() : this.Failure(result);
    }
}
