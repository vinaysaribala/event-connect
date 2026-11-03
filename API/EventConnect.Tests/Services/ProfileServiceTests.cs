using EventConnect.Tests.Support;
using EventConnectCore.Models;
using EventConnectService;

namespace EventConnect.Tests.Services;

public sealed class ProfileServiceTests : IAsyncLifetime
{
    private const string Email = "maria@example.com";

    private readonly ServiceTestHost host = new();
    private int userId;

    public async ValueTask InitializeAsync() => userId = await host.RegisterAsync(Email);

    public ValueTask DisposeAsync() => host.DisposeAsync();

    [Fact]
    public async Task Get_returns_the_profile_with_time_zone()
    {
        var profile = await host.ProfileAsync(p => p.GetAsync(ServiceTestHost.PrincipalFor(userId)));

        Assert.NotNull(profile);
        Assert.Equal((userId, Email, "Maria Lopez", false, "UTC"), (profile.Id, profile.Email, profile.DisplayName, profile.EmailVerified, profile.TimeZone));
        Assert.Equal([Roles.Organizer], profile.Roles);
    }

    [Fact]
    public async Task Get_for_a_user_that_no_longer_exists_returns_null()
    {
        Assert.Null(await host.ProfileAsync(p => p.GetAsync(ServiceTestHost.PrincipalFor(999))));
    }

    [Fact]
    public async Task Update_saves_display_name_and_IANA_time_zone()
    {
        var before = (await host.FindUserAsync(Email)).UpdatedAt;

        var result = await host.ProfileAsync(p => p.UpdateAsync(ServiceTestHost.PrincipalFor(userId), new("  Maria L.  ", " America/Chicago ")));

        Assert.True(result.Succeeded);
        Assert.Equal("Maria L.", result.Value!.DisplayName);
        Assert.Equal("America/Chicago", result.Value.TimeZone);
        var user = await host.FindUserAsync(Email);
        Assert.Equal("Maria L.", user.DisplayName);
        Assert.Equal("America/Chicago", user.TimeZone);
        Assert.True(user.UpdatedAt >= before);
    }

    [Theory]
    [InlineData("Central Standard Time")] // a Windows id, not IANA
    [InlineData("Mars/Olympus_Mons")]
    public async Task Update_with_an_unknown_time_zone_is_invalid(string timeZone)
    {
        var result = await host.ProfileAsync(p => p.UpdateAsync(ServiceTestHost.PrincipalFor(userId), new("Maria", timeZone)));

        Assert.Equal(ServiceResultStatus.Invalid, result.Outcome.Status);
        Assert.Contains("TimeZone", result.Outcome.Errors.Keys);
        Assert.Equal("UTC", (await host.FindUserAsync(Email)).TimeZone);
    }

    [Fact]
    public async Task Update_for_a_user_that_no_longer_exists_is_unauthorized()
    {
        var result = await host.ProfileAsync(p => p.UpdateAsync(ServiceTestHost.PrincipalFor(999), new("Maria", "UTC")));

        Assert.Equal(ServiceResultStatus.Unauthorized, result.Outcome.Status);
    }
}
