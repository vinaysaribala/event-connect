namespace EventConnectService.Interfaces;

public record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>Delivers one email through a provider (SMTP today). Swap the implementation to change provider.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
