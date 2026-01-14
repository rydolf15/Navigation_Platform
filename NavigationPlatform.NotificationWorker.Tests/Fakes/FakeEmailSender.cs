using NavigationPlatform.NotificationWorker.Messaging;

namespace NavigationPlatform.NotificationWorker.Tests.Fakes;

internal sealed class FakeEmailSender : IEmailSender
{
    public List<Guid> EmailsSent { get; } = new();
    public List<(Guid UserId, string Subject, string Body)> Emails { get; } = new();

    public Task SendAsync(Guid userId, string subject, string body)
    {
        EmailsSent.Add(userId);
        Emails.Add((userId, subject, body));
        return Task.CompletedTask;
    }
}
