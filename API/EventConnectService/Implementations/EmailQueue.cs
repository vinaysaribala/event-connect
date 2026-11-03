using System.Threading.Channels;
using EventConnectService.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventConnectService.Implementations;

/// <summary>In-memory queue; emails still queued when the API stops are lost.</summary>
public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> channel = Channel.CreateUnbounded<EmailMessage>();

    public ChannelReader<EmailMessage> Reader => channel.Reader;

    public void Enqueue(EmailMessage message) => channel.Writer.TryWrite(message);
}

/// <summary>Sends queued emails one at a time; a failed send is logged and dropped.</summary>
public class EmailDispatcher(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send email \"{Subject}\" to {To}", message.Subject, message.To);
            }
        }
    }
}
