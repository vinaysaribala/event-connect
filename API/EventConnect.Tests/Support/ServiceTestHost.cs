using System.Security.Claims;
using EventConnectCore.Data;
using EventConnectCore.Models;
using EventConnectService;
using EventConnectService.Implementations;
using EventConnectService.Interfaces;
using EventConnectService.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventConnect.Tests.Support;

/// <summary>
/// The service layer as the API wires it (real Identity), on an in-memory SQLite database, with a fake email
/// queue. Each call runs in its own scope, like a separate request.
/// </summary>
public sealed class ServiceTestHost : IAsyncDisposable
{
    public const string BaseUrl = "http://localhost:5173";
    public const string SeedEmail = "admin@example.com";

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly ServiceProvider provider;

    /// <param name="tokenLifespan">
    /// Overrides how long email-link tokens last. Identity reads the system clock directly, so expiry is tested
    /// with a lifespan of zero rather than by moving a clock.
    /// </param>
    public ServiceTestHost(TimeSpan? tokenLifespan = null)
    {
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = BaseUrl,
                ["Admin:SeedEmail"] = SeedEmail
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<EventConnectDbContext>(options => options.UseSqlite(connection));
        services.AddEventConnectServices(configuration);
        services.AddSingleton<IEmailQueue>(Emails);
        if (tokenLifespan is { } lifespan)
        {
            services.PostConfigure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = lifespan);
            services.PostConfigure<EmailConfirmationTokenProviderOptions>(o => o.TokenLifespan = lifespan);
        }
        provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<EventConnectDbContext>().Database.EnsureCreated();
    }

    public FakeEmailQueue Emails { get; } = new();

    public AdminOptions AdminOptions => provider.GetRequiredService<IOptions<AdminOptions>>().Value;

    /// <summary>The HTTP context of the most recent call, e.g. to see the cookie a sign-in set.</summary>
    public HttpContext? LastHttpContext { get; private set; }

    public Task<T> AuthAsync<T>(Func<IAuthService, Task<T>> action) => RunAsync(action);

    public Task AuthAsync(Func<IAuthService, Task> action) => RunAsync<IAuthService, bool>(async s => { await action(s); return true; });

    public Task<T> ProfileAsync<T>(Func<IProfileService, Task<T>> action) => RunAsync(action);

    public Task<T> UsersAsync<T>(Func<UserManager<User>, Task<T>> action) => RunAsync(action);

    public async Task<User> FindUserAsync(string email) =>
        await UsersAsync(users => users.FindByEmailAsync(email)) ?? throw new InvalidOperationException($"No user {email}.");

    /// <summary>Registers a user and returns its id.</summary>
    public async Task<int> RegisterAsync(string email, string password = "correct-horse-42", string displayName = "Maria Lopez")
    {
        var result = await AuthAsync(auth => auth.RegisterAsync(new(email, password, displayName)));
        return result.Value?.Id ?? throw new InvalidOperationException($"Registering {email} failed: {result.Outcome.Status}.");
    }

    public AsyncServiceScope Scope() => provider.CreateAsyncScope();

    /// <summary>A signed-in principal for the user, as the session cookie would produce.</summary>
    public static ClaimsPrincipal PrincipalFor(int userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"));

    private async Task<T> RunAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        await using var scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        LastHttpContext = httpContext;
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }
}
