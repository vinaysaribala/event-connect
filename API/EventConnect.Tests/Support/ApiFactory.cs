using System.Net;
using EventConnectCore.Data;
using EventConnectService.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventConnect.Tests.Support;

/// <summary>
/// The real API pipeline on an in-memory SQLite database, with emails captured instead of sent.
/// Every request gets its own client IP so the per-IP rate limit doesn't leak between tests; send
/// <see cref="ClientIpHeader"/> to pin one.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string ClientIpHeader = "X-Test-Client-Ip";
    public const string SeedEmail = "admin@example.com";

    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        connection.Open();
        ClientOptions.BaseAddress = new Uri("https://localhost");
        ClientOptions.AllowAutoRedirect = false;
    }

    public FakeEmailQueue Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, so user secrets (real SMTP settings) are never loaded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:EventConnect", "unused");
        builder.UseSetting("App:BaseUrl", "http://localhost:5173");
        builder.UseSetting("Admin:SeedEmail", SeedEmail);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<EventConnectDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EventConnectDbContext>>();
            services.AddDbContext<EventConnectDbContext>(options => options.UseSqlite(connection));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<IEmailQueue>();
            services.AddSingleton<IEmailQueue>(Emails);
            services.AddSingleton<IStartupFilter, ClientIpStartupFilter>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<EventConnectDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }

    private sealed class ClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = context.Request.Headers.TryGetValue(ClientIpHeader, out var ip)
                    ? IPAddress.Parse(ip.ToString())
                    : new IPAddress(Random.Shared.NextInt64(0x0B000000, 0x0BFFFFFF));
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
