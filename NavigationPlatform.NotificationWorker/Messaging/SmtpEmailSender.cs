using Serilog;

namespace NavigationPlatform.NotificationWorker.Messaging;

internal sealed class SmtpEmailSender : IEmailSender
{
    public Task SendAsync(Guid userId, string subject, string body)
    {
        // TEMPORARY IMPLEMENTATION
        // Replace with real SMTP / SendGrid later

        Log.Information(
            "EMAIL fallback → UserId={UserId}, Subject={Subject}, Body={Body}",
            userId,
            subject,
            body);

        return Task.CompletedTask;
    }
}
