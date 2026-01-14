using NavigationPlatform.NotificationWorker.Messaging;

namespace NavigationPlatform.NotificationWorker.Tests.Fakes;

internal sealed class FakeSignalRNotifier : ISignalRNotifier
{
    public List<Guid> NotifiedUsers { get; } = new();
    public List<(Guid UserId, string EventType, object Payload)> Notifications { get; } = new();

    public Task NotifyAsync(Guid userId, string eventType, object payload)
    {
        NotifiedUsers.Add(userId);
        Notifications.Add((userId, eventType, payload));
        return Task.CompletedTask;
    }
}

