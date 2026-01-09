using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.NotificationWorker.Messaging;
using NavigationPlatform.NotificationWorker.Persistence;
using System.Text.Json;

namespace NavigationPlatform.NotificationWorker.Processing;

internal sealed class NotificationOutboxProcessor
{
    private readonly NotificationDbContext _db;
    private readonly IUserPresence _presence;
    private readonly SignalRNotifier _signalr;
    private readonly IEmailSender _email;

    public NotificationOutboxProcessor(
        NotificationDbContext db,
        IUserPresence presence,
        SignalRNotifier signalr,
        IEmailSender email)
    {
        _db = db;
        _presence = presence;
        _signalr = signalr;
        _email = email;
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        var messages = await _db.OutboxMessages
            .Where(x =>
                !x.Processed &&
                (x.Type == nameof(JourneyUpdated) ||
                 x.Type == nameof(JourneyDeleted)))
            .OrderBy(x => x.OccurredUtc)
            .Take(100)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            await HandleAsync(msg, ct);
            msg.Processed = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleAsync(
        Infrastructure.Persistence.Outbox.OutboxMessage msg,
        CancellationToken ct)
    {
        IJourneyEvent evt = msg.Type switch
        {
            nameof(JourneyUpdated) =>
                JsonSerializer.Deserialize<JourneyUpdated>(msg.Payload)!,

            nameof(JourneyDeleted) =>
                JsonSerializer.Deserialize<JourneyDeleted>(msg.Payload)!,

            _ => throw new InvalidOperationException(
                $"Unsupported event type: {msg.Type}")
        };


        var recipients = await _db.JourneyFavourites
            .Where(x => x.JourneyId == evt.JourneyId)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in recipients)
        {
            if (_presence.IsOnline(userId))
            {
                await _signalr.NotifyAsync(
                    userId,
                    msg.Type,
                    new { evt.JourneyId });
            }
            else
            {
                await _email.SendAsync(
                    userId,
                    "Journey update",
                    $"Journey {evt.JourneyId} was updated");
            }
        }
    }
}
