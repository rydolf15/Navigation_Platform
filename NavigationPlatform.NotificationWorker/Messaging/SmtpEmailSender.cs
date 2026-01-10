using Serilog;
using System.Net.Mail;

namespace NavigationPlatform.NotificationWorker.Messaging;

internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(Guid userId, string subject, string body)
    {
        var host = _config["Smtp:Host"]!;
        var port = int.Parse(_config["Smtp:Port"]!);

        using var client = new SmtpClient(host, port);
        var message = new MailMessage(
            "noreply@navigation.local",
            $"{userId}@example.com",
            subject,
            body);

        await client.SendMailAsync(message);
    }
}
