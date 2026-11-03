using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventConnect.Tests.Support;

namespace EventConnect.Tests.Api;

/// <summary>The HTTP contract in docs/requirements/01-accounts.md: routes, status codes and body shapes.</summary>
public sealed class AccountsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct-horse-42";
    private const string GenericLoginError = "Email or password is incorrect";

    // POST /api/auth/register

    [Fact]
    public async Task Register_returns_201_with_the_sample_shape_and_signs_in()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName = "Maria Lopez" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/me", response.Headers.Location?.OriginalString);
        var body = await ReadJsonAsync(response);
        AssertProperties(body, "id", "email", "displayName", "emailVerified");
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Maria Lopez", body.GetProperty("displayName").GetString());
        Assert.False(body.GetProperty("emailVerified").GetBoolean());
        AssertSessionCookie(response);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Single(factory.Emails.To(email));
    }

    [Theory]
    [InlineData("not-an-email", Password, "Maria", "Email")]
    [InlineData("valid@example.com", "123456789", "Maria", "Password")]
    [InlineData("valid@example.com", Password, null, "DisplayName")]
    public async Task Register_with_invalid_input_returns_400_naming_the_field(string email, string password, string? displayName, string field)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password, displayName });

        await AssertValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task Register_with_an_existing_email_in_any_case_returns_409()
    {
        var email = await RegisterAsync(factory.CreateClient());

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = Password, displayName = "Again" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("An account with this email already exists.", (await ReadJsonAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Registering_with_the_seed_email_gives_the_Administrator_role()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { email = ApiFactory.SeedEmail, password = Password, displayName = "Admin" });

        var me = await ReadJsonAsync(await client.GetAsync("/api/me"));

        Assert.Equal(["Administrator", "Organizer"], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    // POST /api/auth/verify-email and /api/auth/resend-verification

    [Fact]
    public async Task Verify_email_returns_204_once_then_400()
    {
        var client = factory.CreateClient();
        var email = await RegisterAsync(client);
        var token = factory.Emails.LatestToken(email, "verify-email");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode);
        Assert.True((await ReadJsonAsync(await client.GetAsync("/api/me"))).GetProperty("emailVerified").GetBoolean());
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/api/auth/verify-email", new { token }), "Token");
    }

    [Fact]
    public async Task Verify_email_needs_no_session()
    {
        var email = await RegisterAsync(factory.CreateClient());

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/verify-email", new { token = factory.Emails.LatestToken(email, "verify-email") });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData(null)]
    public async Task Verify_email_with_a_bad_token_returns_400(string? token)
    {
        await AssertValidationProblemAsync(await factory.CreateClient().PostAsJsonAsync("/api/auth/verify-email", new { token }), "Token");
    }

    [Fact]
    public async Task Resend_verification_returns_202_and_sends_a_new_link()
    {
        var client = factory.CreateClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsync("/api/auth/resend-verification", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(2, factory.Emails.To(email).Count);
    }

    [Fact]
    public async Task Resend_verification_signed_out_returns_401()
    {
        AssertUnauthorizedWithoutRedirect(await factory.CreateClient().PostAsync("/api/auth/resend-verification", null));
    }

    // POST /api/auth/login and /api/auth/logout, GET /api/me

    [Fact]
    public async Task Login_returns_200_with_the_sample_shape_and_sets_the_cookie()
    {
        var email = await RegisterAsync(factory.CreateClient());
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        AssertProperties(body, "id", "email", "displayName", "emailVerified", "roles");
        Assert.Equal(["Organizer"], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        AssertSessionCookie(response);

        var me = await ReadJsonAsync(await client.GetAsync("/api/me"));
        AssertProperties(me, "id", "email", "displayName", "emailVerified", "roles", "timeZone");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("UTC", me.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_401()
    {
        var email = await RegisterAsync(factory.CreateClient());

        var wrongPassword = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password-1" });
        var unknownEmail = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = NewEmail(), password = Password });

        foreach (var response in new[] { wrongPassword, unknownEmail })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(GenericLoginError, (await ReadJsonAsync(response)).GetProperty("title").GetString());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    [Fact]
    public async Task A_locked_account_still_gets_the_generic_401()
    {
        var email = await RegisterAsync(factory.CreateClient());
        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password-1" });
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(GenericLoginError, (await ReadJsonAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Logout_returns_204_and_ends_the_session()
    {
        var client = factory.CreateClient();
        await RegisterAsync(client);

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertUnauthorizedWithoutRedirect(await client.GetAsync("/api/me"));
    }

    // POST /api/auth/forgot-password and /api/auth/reset-password

    [Fact]
    public async Task Forgot_password_returns_202_for_known_and_unknown_emails_but_only_emails_the_known_one()
    {
        var email = await RegisterAsync(factory.CreateClient());
        var unknown = NewEmail();

        var known = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var notKnown = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = unknown });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, notKnown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await notKnown.Content.ReadAsStringAsync());
        Assert.Contains(factory.Emails.To(email), m => m.Subject == "Reset your EventConnect password");
        Assert.Empty(factory.Emails.To(unknown));
    }

    [Fact]
    public async Task Forgot_password_with_a_malformed_email_returns_400()
    {
        await AssertValidationProblemAsync(await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = "nope" }), "Email");
    }

    [Fact]
    public async Task Reset_password_returns_204_once_and_replaces_the_old_password()
    {
        var email = await RegisterAsync(factory.CreateClient());
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = factory.Emails.LatestToken(email, "reset-password");

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "another-strong-pass" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "another-strong-pass" })).StatusCode);
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "yet-another-pass" }), "Token");
    }

    [Fact]
    public async Task Reset_password_with_a_weak_password_returns_400_on_NewPassword()
    {
        var email = await RegisterAsync(factory.CreateClient());
        await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new { token = factory.Emails.LatestToken(email, "reset-password"), newPassword = "short" });

        await AssertValidationProblemAsync(response, "NewPassword");
    }

    // GET/PUT /api/me

    [Fact]
    public async Task Me_signed_out_returns_401_not_a_redirect()
    {
        var client = factory.CreateClient();

        AssertUnauthorizedWithoutRedirect(await client.GetAsync("/api/me"));
        AssertUnauthorizedWithoutRedirect(await client.PutAsJsonAsync("/api/me", new { displayName = "Maria", timeZone = "UTC" }));
    }

    [Fact]
    public async Task Update_profile_returns_200_with_the_updated_profile()
    {
        var client = factory.CreateClient();
        await RegisterAsync(client);

        var response = await client.PutAsJsonAsync("/api/me", new { displayName = "Maria L.", timeZone = "America/Chicago" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        AssertProperties(body, "id", "email", "displayName", "emailVerified", "roles", "timeZone");
        Assert.Equal("Maria L.", body.GetProperty("displayName").GetString());
        Assert.Equal("America/Chicago", body.GetProperty("timeZone").GetString());
        Assert.Equal("America/Chicago", (await ReadJsonAsync(await client.GetAsync("/api/me"))).GetProperty("timeZone").GetString());
    }

    [Theory]
    [InlineData("Maria", "Not/AZone", "TimeZone")]
    [InlineData("Maria", "Eastern Standard Time", "TimeZone")]
    [InlineData("", "UTC", "DisplayName")]
    public async Task Update_profile_with_invalid_input_returns_400(string displayName, string timeZone, string field)
    {
        var client = factory.CreateClient();
        await RegisterAsync(client);

        await AssertValidationProblemAsync(await client.PutAsJsonAsync("/api/me", new { displayName, timeZone }), field);
    }

    // Abuse protection (FRD §16)

    [Fact]
    public async Task Auth_endpoints_allow_20_requests_per_minute_per_IP()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiFactory.ClientIpHeader, "10.20.30.40");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 21; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "x@example.com" })).StatusCode);
        }

        Assert.All(statuses.Take(20), s => Assert.Equal(HttpStatusCode.Accepted, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[20]);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>Registers a new account on <paramref name="client"/>, which is then signed in as it.</summary>
    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = NewEmail();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName = "Maria Lopez" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static void AssertProperties(JsonElement body, params string[] names) =>
        Assert.Equal(names.Order(), body.EnumerateObject().Select(p => p.Name).Order());

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"Expected errors.{field} in {errors}");
    }

    private static void AssertUnauthorizedWithoutRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    private static void AssertSessionCookie(HttpResponseMessage response)
    {
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        var attributes = cookie.ToLowerInvariant();
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.DoesNotContain("expires=", attributes); // browser-session cookie: no "remember me"
    }
}
