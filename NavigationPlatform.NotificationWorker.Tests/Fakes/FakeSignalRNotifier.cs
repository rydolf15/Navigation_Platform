using NavigationPlatform.NotificationWorker.Messaging;

namespace NavigationPlatform.NotificationWorker.Tests.Fakes;

internal sealed class FakeSignalRNotifier : ISignalRNotifier
{
    public List<Guid> NotifiedUsers { get; } = new();

    public Task NotifyAsync(Guid userId, string _, object __)
    {
        NotifiedUsers.Add(userId);
        return Task.CompletedTask;
    }
}

