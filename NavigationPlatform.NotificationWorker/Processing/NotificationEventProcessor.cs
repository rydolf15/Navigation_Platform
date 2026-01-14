using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.NotificationWorker.Messaging;
using NavigationPlatform.NotificationWorker.Persistence;
using System.Text.Json;

namespace NavigationPlatform.NotificationWorker.Processing;

internal sealed class NotificationEventProcessor
{
    private readonly NotificationDbContext _db;
    private readonly IUserPresence _presence;
    private readonly ISignalRNotifier _notifier;
    private readonly IEmailSender _email;

    public NotificationEventProcessor(
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

    public async Task ProcessAsync(
        Guid messageId,
        string type,
        string payloadJson,
        CancellationToken ct)
    {
        // Idempotence: RabbitMQ is at-least-once.
        var alreadyProcessed = await _db.InboxMessages
            .AsNoTracking()
            .AnyAsync(x => x.Id == messageId, ct);

        if (alreadyProcessed)
            return;

        switch (type)
        {
            case nameof(JourneyFavorited):
                {
                    var evt = JsonSerializer.Deserialize<JourneyFavorited>(payloadJson)!;

                    await UpsertFavouriteAsync(evt.JourneyId, evt.UserId, isFavourite: true, ct);

                    await NotifyUserAsync(
                        evt.UserId,
                        NotificationEvents.JourneyFavouriteChanged,
                        new { journeyId = evt.JourneyId, isFavourite = true },
                        ct);

                    break;
                }

            case nameof(JourneyUnfavorited):
                {
                    var evt = JsonSerializer.Deserialize<JourneyUnfavorited>(payloadJson)!;

                    await UpsertFavouriteAsync(evt.JourneyId, evt.UserId, isFavourite: false, ct);

                    await NotifyUserAsync(
                        evt.UserId,
                        NotificationEvents.JourneyFavouriteChanged,
                        new { journeyId = evt.JourneyId, isFavourite = false },
                        ct);

                    break;
                }

            case nameof(JourneyUpdated):
                {
                    var evt = JsonSerializer.Deserialize<JourneyUpdated>(payloadJson)!;

                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        NotificationEvents.JourneyUpdated,
                        new { evt.JourneyId },
                        ct);

                    break;
                }

            case nameof(JourneyDeleted):
                {
                    var evt = JsonSerializer.Deserialize<JourneyDeleted>(payloadJson)!;

                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        NotificationEvents.JourneyDeleted,
                        new { evt.JourneyId },
                        ct);

                    break;
                }

            case nameof(JourneyShared):
                {
                    var evt = JsonSerializer.Deserialize<JourneyShared>(payloadJson)!;

                    // Only notify if there's a specific user recipient (not public link)
                    if (evt.SharedWithUserId.HasValue)
                    {
                        await NotifyUserAsync(
                            evt.SharedWithUserId.Value,
                            NotificationEvents.JourneyShared,
                            new { evt.JourneyId },
                            ct);
                    }

                    break;
                }

            case nameof(JourneyDailyGoalAchieved):
                {
                    var evt = JsonSerializer.Deserialize<JourneyDailyGoalAchieved>(payloadJson)!;

                    await NotifyUserAsync(
                        evt.UserId,
                        NotificationEvents.JourneyDailyGoalAchieved,
                        new
                        {
                            date = evt.Date,
                            totalDistanceKm = evt.TotalDistanceKm
                        },
                        ct);

                    break;
                }

            default:
                // Unknown event type; ignore but still mark processed to avoid poison-message loops.
                break;
        }

        _db.InboxMessages.Add(new InboxMessage
        {
            Id = messageId,
            Type = type,
            OccurredUtc = DateTime.UtcNow,
            ProcessedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertFavouriteAsync(Guid journeyId, Guid userId, bool isFavourite, CancellationToken ct)
    {
        var existing = await _db.JourneyFavourites.FindAsync([journeyId, userId], ct);

        if (isFavourite)
        {
            if (existing == null)
            {
                _db.JourneyFavourites.Add(new JourneyFavourite
                {
                    JourneyId = journeyId,
                    UserId = userId
                });
            }
        }
        else
        {
            if (existing != null)
            {
                _db.JourneyFavourites.Remove(existing);
            }
        }
    }

    private async Task NotifyUserAsync(Guid userId, string eventType, object payload, CancellationToken ct)
    {
        if (_presence.IsOnline(userId))
        {
            await _notifier.NotifyAsync(userId, eventType, payload);
            return;
        }

        await _email.SendAsync(
            userId,
            "Journey notification",
            $"Event: {eventType}");
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
            await NotifyUserAsync(userId, eventType, payload, ct);
        }
    }
}

