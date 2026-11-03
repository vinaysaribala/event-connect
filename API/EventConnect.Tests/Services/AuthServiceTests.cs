using EventConnect.Tests.Support;
using EventConnectCore.Models;
using EventConnectService;
using EventConnectService.Implementations;
using EventConnectService.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventConnect.Tests.Services;

public sealed class AuthServiceTests : IAsyncLifetime
{
    private const string Email = "maria@example.com";
    private const string Password = "correct-horse-42";

    private readonly ServiceTestHost host = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => host.DisposeAsync();

    // FR-001: register

    [Fact]
    public async Task Register_creates_an_unverified_organizer_on_UTC()
    {
        var result = await host.AuthAsync(auth => auth.RegisterAsync(new(Email, Password, "Maria Lopez")));

        Assert.True(result.Succeeded);
        Assert.Equal(new RegisteredUserViewModel(result.Value!.Id, Email, "Maria Lopez", false), result.Value);

        var user = await host.FindUserAsync(Email);
        Assert.False(user.EmailConfirmed);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Equal("UTC", user.TimeZone);
        Assert.Equal([Roles.Organizer], await host.UsersAsync(users => users.GetRolesAsync(user)));
    }

    [Fact]
    public async Task Register_trims_email_and_display_name()
    {
        var result = await host.AuthAsync(auth => auth.RegisterAsync(new($"  {Email} ", Password, "  Maria Lopez  ")));

        Assert.Equal(Email, result.Value!.Email);
        Assert.Equal("Maria Lopez", result.Value.DisplayName);
    }

    [Fact]
    public async Task Register_queues_a_verification_email_with_a_link_to_the_UI()
    {
        await host.RegisterAsync(Email);

        var message = Assert.Single(host.Emails.To(Email));
        Assert.Equal("Verify your EventConnect email", message.Subject);
        Assert.Contains($"{ServiceTestHost.BaseUrl}/verify-email?token=", message.TextBody);
        Assert.Contains($"{ServiceTestHost.BaseUrl}/verify-email?token=", message.HtmlBody);
        Assert.Contains("24 hours", message.TextBody);
    }

    [Fact]
    public async Task Register_signs_the_new_user_in()
    {
        await host.RegisterAsync(Email);

        Assert.Contains(host.LastHttpContext!.Response.Headers.SetCookie, c => c!.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("maria@example.com")]
    [InlineData("MARIA@Example.com")]
    public async Task Register_with_an_existing_email_in_any_case_is_a_conflict(string again)
    {
        await host.RegisterAsync(Email);

        var result = await host.AuthAsync(auth => auth.RegisterAsync(new(again, Password, "Someone Else")));

        Assert.Equal(ServiceResultStatus.Conflict, result.Outcome.Status);
        Assert.Equal("An account with this email already exists.", result.Outcome.Message);
        Assert.Single(host.Emails.Messages);
    }

    [Fact]
    public async Task Register_with_a_short_password_is_invalid_on_the_password_field()
    {
        var result = await host.AuthAsync(auth => auth.RegisterAsync(new(Email, "123456789", "Maria Lopez")));

        Assert.Equal(ServiceResultStatus.Invalid, result.Outcome.Status);
        Assert.Contains("Password", result.Outcome.Errors.Keys);
        Assert.Empty(host.Emails.Messages);
    }

    // Roles: the seed administrator

    [Theory]
    [InlineData(ServiceTestHost.SeedEmail)]
    [InlineData("Admin@Example.com")]
    public async Task Registering_with_the_seed_email_grants_Administrator(string email)
    {
        var result = await host.AuthAsync(auth => auth.RegisterAsync(new(email, Password, "Admin")));

        var roles = await host.UsersAsync(async users => await users.GetRolesAsync((await users.FindByIdAsync(result.Value!.Id.ToString()))!));
        Assert.Equal([Roles.Administrator, Roles.Organizer], roles.Order());
    }

    [Fact]
    public async Task Verifying_with_the_seed_email_grants_Administrator()
    {
        var id = await host.RegisterAsync("late-admin@example.com");
        host.AdminOptions.SeedEmail = "late-admin@example.com";

        await host.AuthAsync(auth => auth.VerifyEmailAsync(new(host.Emails.LatestToken("late-admin@example.com", "verify-email"))));

        var user = await host.FindUserAsync("late-admin@example.com");
        Assert.True(await host.UsersAsync(users => users.IsInRoleAsync(user, Roles.Administrator)));
        Assert.Equal(id, user.Id);
    }

    // FR-001: verify email

    [Fact]
    public async Task Verify_with_the_emailed_link_confirms_the_email()
    {
        await host.RegisterAsync(Email);

        var result = await host.AuthAsync(auth => auth.VerifyEmailAsync(new(host.Emails.LatestToken(Email, "verify-email"))));

        Assert.True(result.Succeeded);
        var user = await host.FindUserAsync(Email);
        Assert.True(user.EmailConfirmed);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.InRange(user.EmailVerifiedAt.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Verify_link_works_only_once()
    {
        await host.RegisterAsync(Email);
        var token = host.Emails.LatestToken(Email, "verify-email");
        await host.AuthAsync(auth => auth.VerifyEmailAsync(new(token)));

        var second = await host.AuthAsync(auth => auth.VerifyEmailAsync(new(token)));

        AssertInvalidLink(second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("OTk5OnNvbWV0aGluZw")] // "999:something": unknown user
    [InlineData("MTpub3QtYS1yZWFsLXRva2Vu")] // "1:not-a-real-token": real user, forged token
    public async Task Verify_with_a_malformed_or_forged_token_is_invalid(string token)
    {
        await host.RegisterAsync(Email);

        AssertInvalidLink(await host.AuthAsync(auth => auth.VerifyEmailAsync(new(token))));
        Assert.False((await host.FindUserAsync(Email)).EmailConfirmed);
    }

    [Fact]
    public async Task Verify_with_a_password_reset_token_is_invalid()
    {
        await host.RegisterAsync(Email);
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));

        var result = await host.AuthAsync(auth => auth.VerifyEmailAsync(new(host.Emails.LatestToken(Email, "reset-password"))));

        AssertInvalidLink(result);
    }

    [Fact]
    public async Task Verify_with_an_expired_link_is_invalid()
    {
        await using var expiring = new ServiceTestHost(tokenLifespan: TimeSpan.Zero);
        await expiring.RegisterAsync(Email);
        await Task.Delay(20);

        var result = await expiring.AuthAsync(auth => auth.VerifyEmailAsync(new(expiring.Emails.LatestToken(Email, "verify-email"))));

        AssertInvalidLink(result);
    }

    [Fact]
    public async Task Link_tokens_last_24_hours_for_verification_and_1_hour_for_reset()
    {
        await using var scope = host.Scope();

        Assert.Equal(TimeSpan.FromHours(24), scope.ServiceProvider.GetRequiredService<IOptions<EmailConfirmationTokenProviderOptions>>().Value.TokenLifespan);
        Assert.Equal(TimeSpan.FromHours(1), scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan);
    }

    // FR-001: re-request the verification link

    [Fact]
    public async Task Resend_verification_queues_a_new_working_link()
    {
        var id = await host.RegisterAsync(Email);

        await host.AuthAsync(auth => auth.ResendVerificationAsync(ServiceTestHost.PrincipalFor(id)));

        Assert.Equal(2, host.Emails.To(Email).Count);
        Assert.True((await host.AuthAsync(auth => auth.VerifyEmailAsync(new(host.Emails.LatestToken(Email, "verify-email"))))).Succeeded);
    }

    [Fact]
    public async Task Resend_verification_does_nothing_once_verified()
    {
        var id = await host.RegisterAsync(Email);
        await host.AuthAsync(auth => auth.VerifyEmailAsync(new(host.Emails.LatestToken(Email, "verify-email"))));

        await host.AuthAsync(auth => auth.ResendVerificationAsync(ServiceTestHost.PrincipalFor(id)));

        Assert.Single(host.Emails.To(Email));
    }

    // FR-002: sign in

    [Fact]
    public async Task Login_with_correct_credentials_returns_the_user_and_roles()
    {
        var id = await host.RegisterAsync(Email);

        var user = await host.AuthAsync(auth => auth.LoginAsync(new(Email, Password)));

        Assert.NotNull(user);
        Assert.Equal(id, user.Id);
        Assert.Equal(Email, user.Email);
        Assert.False(user.EmailVerified);
        Assert.Equal([Roles.Organizer], user.Roles);
        Assert.Contains(host.LastHttpContext!.Response.Headers.SetCookie, c => c!.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_ignores_email_case_and_surrounding_spaces()
    {
        await host.RegisterAsync(Email);

        Assert.NotNull(await host.AuthAsync(auth => auth.LoginAsync(new(" MARIA@example.com ", Password))));
    }

    [Theory]
    [InlineData(Email, "wrong-password-1")]
    [InlineData("nobody@example.com", Password)]
    public async Task Login_with_a_wrong_password_or_unknown_email_returns_null(string email, string password)
    {
        await host.RegisterAsync(Email);

        Assert.Null(await host.AuthAsync(auth => auth.LoginAsync(new(email, password))));
    }

    [Fact]
    public async Task Login_locks_the_account_after_five_wrong_passwords()
    {
        await host.RegisterAsync(Email);
        for (var i = 0; i < 5; i++)
        {
            await host.AuthAsync(auth => auth.LoginAsync(new(Email, "wrong-password-1")));
        }

        Assert.Null(await host.AuthAsync(auth => auth.LoginAsync(new(Email, Password))));
        Assert.True(await host.UsersAsync(async users => await users.IsLockedOutAsync((await users.FindByEmailAsync(Email))!)));
    }

    // FR-002: forgot and reset password

    [Fact]
    public async Task Forgot_password_queues_a_reset_link_for_a_known_email()
    {
        await host.RegisterAsync(Email);

        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email.ToUpperInvariant())));

        var message = host.Emails.To(Email)[^1];
        Assert.Equal("Reset your EventConnect password", message.Subject);
        Assert.Contains($"{ServiceTestHost.BaseUrl}/reset-password?token=", message.TextBody);
        Assert.Contains("expires in 1 hour", message.TextBody);
    }

    [Fact]
    public async Task Forgot_password_sends_nothing_for_an_unknown_email()
    {
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new("nobody@example.com")));

        Assert.Empty(host.Emails.Messages);
    }

    [Fact]
    public async Task Reset_password_changes_the_password()
    {
        await host.RegisterAsync(Email);
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));

        var result = await host.AuthAsync(auth => auth.ResetPasswordAsync(new(host.Emails.LatestToken(Email, "reset-password"), "another-strong-pass")));

        Assert.True(result.Succeeded);
        Assert.Null(await host.AuthAsync(auth => auth.LoginAsync(new(Email, Password))));
        Assert.NotNull(await host.AuthAsync(auth => auth.LoginAsync(new(Email, "another-strong-pass"))));
    }

    [Fact]
    public async Task Reset_link_works_only_once()
    {
        await host.RegisterAsync(Email);
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));
        var token = host.Emails.LatestToken(Email, "reset-password");
        await host.AuthAsync(auth => auth.ResetPasswordAsync(new(token, "another-strong-pass")));

        var second = await host.AuthAsync(auth => auth.ResetPasswordAsync(new(token, "yet-another-pass")));

        AssertInvalidLink(second);
        Assert.NotNull(await host.AuthAsync(auth => auth.LoginAsync(new(Email, "another-strong-pass"))));
    }

    [Fact]
    public async Task Reset_invalidates_other_outstanding_links()
    {
        await host.RegisterAsync(Email);
        var verifyToken = host.Emails.LatestToken(Email, "verify-email");
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));
        var olderReset = host.Emails.LatestToken(Email, "reset-password");
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));

        await host.AuthAsync(auth => auth.ResetPasswordAsync(new(host.Emails.LatestToken(Email, "reset-password"), "another-strong-pass")));

        AssertInvalidLink(await host.AuthAsync(auth => auth.ResetPasswordAsync(new(olderReset, "yet-another-pass"))));
        AssertInvalidLink(await host.AuthAsync(auth => auth.VerifyEmailAsync(new(verifyToken))));
    }

    [Fact]
    public async Task Reset_with_an_expired_link_is_invalid()
    {
        await using var expiring = new ServiceTestHost(tokenLifespan: TimeSpan.Zero);
        await expiring.RegisterAsync(Email);
        await expiring.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));
        await Task.Delay(20);

        var result = await expiring.AuthAsync(auth => auth.ResetPasswordAsync(new(expiring.Emails.LatestToken(Email, "reset-password"), "another-strong-pass")));

        AssertInvalidLink(result);
    }

    [Fact]
    public async Task Reset_with_a_verification_token_is_invalid()
    {
        await host.RegisterAsync(Email);

        var result = await host.AuthAsync(auth => auth.ResetPasswordAsync(new(host.Emails.LatestToken(Email, "verify-email"), "another-strong-pass")));

        AssertInvalidLink(result);
    }

    [Fact]
    public async Task Reset_with_a_short_password_is_invalid_on_the_new_password_field()
    {
        await host.RegisterAsync(Email);
        await host.AuthAsync(auth => auth.ForgotPasswordAsync(new(Email)));
        var token = host.Emails.LatestToken(Email, "reset-password");

        var result = await host.AuthAsync(auth => auth.ResetPasswordAsync(new(token, "short")));

        Assert.Equal(ServiceResultStatus.Invalid, result.Status);
        Assert.Contains("NewPassword", result.Errors.Keys);
        Assert.True((await host.AuthAsync(auth => auth.ResetPasswordAsync(new(token, "another-strong-pass")))).Succeeded);
    }

    private static void AssertInvalidLink(ServiceResult result)
    {
        Assert.Equal(ServiceResultStatus.Invalid, result.Status);
        Assert.Equal(["This link is invalid or has expired."], result.Errors["Token"]);
    }
}
