namespace NavigationPlatform.NotificationWorker.Messaging;

public interface IUserPresence
{
    bool IsOnline(Guid userId);
}