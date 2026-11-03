using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using EventConnectService.Interfaces;
using EventConnectService.Options;
using Microsoft.Extensions.Options;

namespace EventConnectService.Implementations;

/// <summary>Sends over SMTP with STARTTLS (Gmail: smtp.gmail.com:587 with an App Password).</summary>
public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;
        using var mail = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress!, smtp.FromName),
            Subject = message.Subject,
            Body = message.TextBody
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtp.UserName, smtp.Password)
        };
        await client.SendMailAsync(mail, cancellationToken);
    }
}
