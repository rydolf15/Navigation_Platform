using NavigationPlatform.NotificationWorker.Messaging;

namespace NavigationPlatform.NotificationWorker.Tests.Fakes;

internal sealed class FakeEmailSender : IEmailSender
{
    public List<Guid> EmailsSent { get; } = new();

    public Task SendAsync(Guid userId, string _, string __)
    {
        EmailsSent.Add(userId);
        return Task.CompletedTask;
    }
}
