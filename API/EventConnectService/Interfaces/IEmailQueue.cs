namespace EventConnectService.Interfaces;

/// <summary>Queues an email for background delivery, so requests don't wait on (or reveal) sending.</summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}
