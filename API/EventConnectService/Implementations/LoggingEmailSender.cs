using EventConnectService.Interfaces;
using Microsoft.Extensions.Logging;

namespace EventConnectService.Implementations;

/// <summary>Used when SMTP isn't configured: writes the email, including its links, to the log.</summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "SMTP is not configured, so this email was logged instead of sent.\nTo: {To}\nSubject: {Subject}\n\n{Body}",
            message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}
