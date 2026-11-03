using EventConnectCore.Data;
using EventConnectCore.Models;
using EventConnectService.Interfaces;
using EventConnectService.Implementations;
using EventConnectService.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventConnectService;

public static class DependencyInjection
{
    public static IServiceCollection AddEventConnectServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .Validate(o => Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute), "App:BaseUrl must be an absolute URL.")
            .ValidateOnStart();
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        services.AddIdentity<User, IdentityRole<int>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = ""; // the user name is the email, so allow any address
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = false; // unverified users can sign in and draft events
                options.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProvider.ProviderName;
            })
            .AddEntityFrameworkStores<EventConnectDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<EmailConfirmationTokenProvider>(EmailConfirmationTokenProvider.ProviderName);
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        var smtp = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>();
        if (smtp?.IsConfigured == true)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddHostedService<EmailDispatcher>();

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
