using EventConnectCore.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventConnectService.Implementations;

/// <summary>
/// Email verification tokens get their own lifespan (24 hours); password reset uses the default provider (1 hour).
/// </summary>
public class EmailConfirmationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "EmailConfirmation";
}

public class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public EmailConfirmationTokenProviderOptions()
    {
        Name = EmailConfirmationTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(24);
    }
}
