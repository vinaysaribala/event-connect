using System.Net;
using System.Security.Claims;
using EventConnectCore.Models;
using EventConnectService.Interfaces;
using EventConnectService.Options;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EventConnectService.Implementations;

public class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IEmailQueue emailQueue,
    IOptions<AppOptions> appOptions,
    IOptions<AdminOptions> adminOptions) : IAuthService
{
    private const string DefaultTimeZone = "UTC";
    private const string InvalidLinkMessage = "This link is invalid or has expired.";

    public async Task<ServiceResult<RegisteredUserViewModel>> RegisterAsync(RegisterViewModel model)
    {
        var email = model.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return EmailTaken();
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserName = email,
            Email = email,
            DisplayName = model.DisplayName.Trim(),
            TimeZone = DefaultTimeZone,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            return created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
                ? EmailTaken()
                : ServiceResult.Invalid(ToErrors(created.Errors));
        }

        await userManager.AddToRoleAsync(user, Roles.Organizer);
        await GrantSeedAdministratorAsync(user);
        await QueueVerificationEmailAsync(user);
        await signInManager.SignInAsync(user, isPersistent: false);

        return new RegisteredUserViewModel(user.Id, email, user.DisplayName, user.EmailConfirmed);
    }

    public async Task<ServiceResult> VerifyEmailAsync(VerifyEmailViewModel model)
    {
        var (user, token) = await FindByLinkTokenAsync(model.Token);
        // Identity's confirmation token stays valid after use, so an already verified user means the link was used.
        if (user is null || user.EmailConfirmed)
        {
            return ServiceResult.Invalid("Token", InvalidLinkMessage);
        }

        var now = DateTime.UtcNow;
        user.EmailVerifiedAt = now;
        user.UpdatedAt = now;
        var confirmed = await userManager.ConfirmEmailAsync(user, token);
        if (!confirmed.Succeeded)
        {
            return ServiceResult.Invalid("Token", InvalidLinkMessage);
        }

        await GrantSeedAdministratorAsync(user);
        return ServiceResult.Success();
    }

    public async Task ResendVerificationAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is not null && !user.EmailConfirmed)
        {
            await QueueVerificationEmailAsync(user);
        }
    }

    public async Task<UserViewModel?> LoginAsync(LoginViewModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            return null;
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? await userManager.ToUserViewModelAsync(user) : null;
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task ForgotPasswordAsync(ForgotPasswordViewModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = BuildLink("reset-password", LinkToken.Create(user.Id, token));
        emailQueue.Enqueue(new EmailMessage(
            user.Email!,
            "Reset your EventConnect password",
            $"""
            Hi {user.DisplayName},

            Someone asked to reset the password for your EventConnect account. To choose a new password, open this link:
            {link}

            The link works once and expires in 1 hour. If you didn't ask for this, you can ignore this email.
            """,
            $"""
            <p>Hi {WebUtility.HtmlEncode(user.DisplayName)},</p>
            <p>Someone asked to reset the password for your EventConnect account.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Choose a new password</a></p>
            <p>The link works once and expires in 1 hour. If you didn't ask for this, you can ignore this email.</p>
            """));
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model)
    {
        var (user, token) = await FindByLinkTokenAsync(model.Token);
        if (user is null)
        {
            return ServiceResult.Invalid("Token", InvalidLinkMessage);
        }

        // Resetting changes the security stamp, which invalidates this link and every other outstanding one.
        var reset = await userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (reset.Succeeded)
        {
            return ServiceResult.Success();
        }

        return reset.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
            ? ServiceResult.Invalid("Token", InvalidLinkMessage)
            : ServiceResult.Invalid(ToErrors(reset.Errors, passwordField: "NewPassword"));
    }

    private async Task<(User? User, string Token)> FindByLinkTokenAsync(string linkToken)
    {
        if (!LinkToken.TryRead(linkToken, out var userId, out var token))
        {
            return (null, "");
        }

        return (await userManager.FindByIdAsync(userId.ToString()), token);
    }

    private async Task QueueVerificationEmailAsync(User user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildLink("verify-email", LinkToken.Create(user.Id, token));
        emailQueue.Enqueue(new EmailMessage(
            user.Email!,
            "Verify your EventConnect email",
            $"""
            Hi {user.DisplayName},

            Welcome to EventConnect. Confirm your email address by opening this link:
            {link}

            The link expires in 24 hours. If you didn't create an EventConnect account, you can ignore this email.
            """,
            $"""
            <p>Hi {WebUtility.HtmlEncode(user.DisplayName)},</p>
            <p>Welcome to EventConnect. Confirm your email address to start publishing events.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Verify my email</a></p>
            <p>The link expires in 24 hours. If you didn't create an EventConnect account, you can ignore this email.</p>
            """));
    }

    private async Task GrantSeedAdministratorAsync(User user)
    {
        var seedEmail = adminOptions.Value.SeedEmail;
        if (!string.IsNullOrWhiteSpace(seedEmail) &&
            string.Equals(user.Email, seedEmail.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !await userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            await userManager.AddToRoleAsync(user, Roles.Administrator);
        }
    }

    private string BuildLink(string path, string token) =>
        $"{appOptions.Value.BaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";

    private static ServiceResult EmailTaken() => ServiceResult.Conflict("An account with this email already exists.");

    private static Dictionary<string, string[]> ToErrors(IEnumerable<IdentityError> errors, string passwordField = "Password") =>
        errors
            .GroupBy(e => e.Code switch
            {
                _ when e.Code.StartsWith("Password", StringComparison.Ordinal) => passwordField,
                nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName) => "Email",
                _ => ""
            })
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
