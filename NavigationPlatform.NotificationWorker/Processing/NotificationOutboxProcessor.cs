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
    private readonly ISignalRNotifier _notifier;
    private readonly IEmailSender _email;

    public NotificationOutboxProcessor(
        NotificationDbContext db,
        IUserPresence presence,
        ISignalRNotifier notifier,
        IEmailSender email)
    {
        _db = db;
        _presence = presence;
        _notifier = notifier;
        _email = email;
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        var messages = await _db.OutboxMessages
            .Where(x =>
                !x.Processed &&
                (x.Type == nameof(JourneyUpdated) ||
                 x.Type == nameof(JourneyDeleted) ||
                 x.Type == nameof(JourneyFavorited) ||
                 x.Type == nameof(JourneyUnfavorited) ||
                 x.Type == nameof(JourneyShared) || 
                 x.Type == nameof(JourneyDailyGoalAchieved)))
            .OrderBy(x => x.OccurredUtc)
            .Take(100)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            try
            {
                await HandleAsync(msg, ct);
                msg.Processed = true;
            }
            catch
            {
                throw;
            }
        }


        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleAsync(
     Infrastructure.Persistence.Outbox.OutboxMessage msg,
     CancellationToken ct)
    {
        switch (msg.Type)
        {
            case nameof(JourneyFavorited):
                {
                    var evt = JsonSerializer.Deserialize<JourneyFavorited>(msg.Payload)!;

                    await NotifyUserAsync(
                        evt.UserId,
                        "JourneyFavouriteChanged",
                        new { journeyId = evt.JourneyId, isFavourite = true });

                    break;
                }

            case nameof(JourneyUnfavorited):
                {
                    var evt = JsonSerializer.Deserialize<JourneyUnfavorited>(msg.Payload)!;

                    await NotifyUserAsync(
                        evt.UserId,
                        "JourneyFavouriteChanged",
                        new { journeyId = evt.JourneyId, isFavourite = false });

                    break;
                }

            case nameof(JourneyUpdated):
                {
                    var evt = JsonSerializer.Deserialize<JourneyUpdated>(msg.Payload)!;

                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        "JourneyUpdated",
                        new { evt.JourneyId },
                        ct);

                    break;
                }

            case nameof(JourneyDeleted):
                {
                    var evt = JsonSerializer.Deserialize<JourneyDeleted>(msg.Payload)!;

                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        "JourneyDeleted",
                        new { evt.JourneyId },
                        ct);

                    break;
                }

            case nameof(JourneyShared):
                {
                    var evt = JsonSerializer.Deserialize<JourneyShared>(msg.Payload)!;

                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        "JourneyShared",
                        new { evt.JourneyId },
                        ct);

                    break;
                }

            case nameof(JourneyDailyGoalAchieved):
                {
                    var evt = JsonSerializer.Deserialize<JourneyDailyGoalAchieved>(msg.Payload)!;

                    await NotifyUserAsync(
                        evt.UserId,
                        "JourneyDailyGoalAchieved",
                        new
                        {
                            date = evt.Date,
                            totalDistanceKm = evt.TotalDistanceKm
                        });

                    break;
                }


            default:
                throw new InvalidOperationException(
                    $"Unsupported event type: {msg.Type}");
        }
    }

    private async Task NotifyUserAsync(
    Guid userId,
    string eventType,
    object payload)
    {
        if (_presence.IsOnline(userId))
        {
            await _notifier.NotifyAsync(userId, eventType, payload);
        }
        if (eventType != NotificationEvents.JourneyDailyGoalAchieved)
        {
            await _email.SendAsync(
                userId,
                "Journey update",
                $"Event: {eventType}");
        }
    }

    private async Task NotifyFavouritersAsync(
        Guid journeyId,
        string eventType,
        object payload,
        CancellationToken ct)
    {
        var recipients = await _db.JourneyFavourites
            .Where(x => x.JourneyId == journeyId)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in recipients)
        {
            await NotifyUserAsync(userId, eventType, payload);
        }
    }

}
