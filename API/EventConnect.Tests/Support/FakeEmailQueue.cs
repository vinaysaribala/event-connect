using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using EventConnectService.Interfaces;

namespace EventConnect.Tests.Support;

/// <summary>Captures queued emails instead of sending them.</summary>
public sealed partial class FakeEmailQueue : IEmailQueue
{
    private readonly ConcurrentQueue<EmailMessage> messages = new();

    public IReadOnlyList<EmailMessage> Messages => [.. messages];

    public void Enqueue(EmailMessage message) => messages.Enqueue(message);

    public IReadOnlyList<EmailMessage> To(string address) =>
        [.. messages.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The link token from the latest email to <paramref name="address"/> whose link goes to <paramref name="path"/>.</summary>
    public string LatestToken(string address, string path)
    {
        var message = To(address).LastOrDefault(m => m.TextBody.Contains($"/{path}?token=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No {path} email was queued for {address}.");
        return TokenFrom(message);
    }

    public static string TokenFrom(EmailMessage message) =>
        Uri.UnescapeDataString(TokenPattern().Match(message.TextBody).Groups[1].Value);

    [GeneratedRegex(@"\?token=(\S+)")]
    private static partial Regex TokenPattern();
}
