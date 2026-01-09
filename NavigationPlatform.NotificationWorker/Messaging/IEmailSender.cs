namespace NavigationPlatform.NotificationWorker.Messaging;

public interface IEmailSender
{
    Task SendAsync(Guid userId, string subject, string body);
}