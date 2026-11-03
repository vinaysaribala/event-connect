using EventConnectService;
using EventConnectService.Implementations;
using EventConnectService.Interfaces;
using EventConnectService.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventConnect.Tests.Services;

public class EmailTests
{
    private static readonly EmailMessage First = new("a@example.com", "First", "text", "<p>html</p>");
    private static readonly EmailMessage Second = new("b@example.com", "Second", "text", "<p>html</p>");

    [Fact]
    public void Smtp_is_used_only_when_host_user_password_and_from_address_are_all_set()
    {
        Assert.IsType<SmtpEmailSender>(SenderFor(new() { ["Email:Smtp:Host"] = "smtp.gmail.com", ["Email:Smtp:UserName"] = "u", ["Email:Smtp:Password"] = "p", ["Email:Smtp:FromAddress"] = "f@example.com" }));
        Assert.IsType<LoggingEmailSender>(SenderFor(new() { ["Email:Smtp:Host"] = "smtp.gmail.com", ["Email:Smtp:UserName"] = "u", ["Email:Smtp:FromAddress"] = "f@example.com" }));
        Assert.IsType<LoggingEmailSender>(SenderFor(new()));
    }

    [Theory]
    [InlineData(null, "u", "p", "f")]
    [InlineData("h", " ", "p", "f")]
    [InlineData("h", "u", "", "f")]
    [InlineData("h", "u", "p", null)]
    public void Smtp_options_are_not_configured_when_any_required_setting_is_blank(string? host, string? user, string? password, string? from)
    {
        Assert.False(new SmtpOptions { Host = host, UserName = user, Password = password, FromAddress = from }.IsConfigured);
    }

    [Fact]
    public async Task Logging_sender_succeeds_without_SMTP()
    {
        await new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance).SendAsync(First);
    }

    [Fact]
    public async Task Dispatcher_sends_queued_emails_and_keeps_going_after_a_failure()
    {
        var queue = new EmailQueue();
        var sender = new FlakySender(failOn: First);
        using var dispatcher = new EmailDispatcher(queue, sender, NullLogger<EmailDispatcher>.Instance);
        await dispatcher.StartAsync(CancellationToken.None);

        queue.Enqueue(First);
        queue.Enqueue(Second);

        Assert.Equal(Second, await sender.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await dispatcher.StopAsync(CancellationToken.None);
    }

    private static IEmailSender SenderFor(Dictionary<string, string?> settings)
    {
        settings["App:BaseUrl"] = "http://localhost:5173";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventConnectServices(configuration);
        return services.BuildServiceProvider().GetRequiredService<IEmailSender>();
    }

    private sealed class FlakySender(EmailMessage failOn) : IEmailSender
    {
        public TaskCompletionSource<EmailMessage> Delivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (message == failOn)
            {
                throw new InvalidOperationException("SMTP is down.");
            }

            Delivered.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
