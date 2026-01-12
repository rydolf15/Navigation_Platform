using NavigationPlatform.NotificationWorker.Messaging;

namespace NavigationPlatform.NotificationWorker.Tests.Fakes;

internal sealed class FakeUserPresence : IUserPresence
{
    private readonly HashSet<Guid> _online = new();

    public void SetOnline(Guid userId) => _online.Add(userId);
    public bool IsOnline(Guid userId) => _online.Contains(userId);
}