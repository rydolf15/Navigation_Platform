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
    private readonly ILogger<NotificationEventProcessor> _logger;

    public NotificationEventProcessor(
        NotificationDbContext db,
        IUserPresence presence,
        ISignalRNotifier notifier,
        IEmailSender email,
        ILogger<NotificationEventProcessor> logger)
    {
        _db = db;
        _presence = presence;
        _notifier = notifier;
        _email = email;
        _logger = logger;
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

                    // Notify the user who favorited (for their own UI update)
                    await NotifyUserAsync(
                        evt.UserId,
                        NotificationEvents.JourneyFavouriteChanged,
                        new { journeyId = evt.JourneyId, isFavourite = true },
                        ct);

                    // Notify the journey owner (if different from the user who favorited)
                    if (evt.JourneyOwnerId != evt.UserId)
                    {
                        await NotifyUserAsync(
                            evt.JourneyOwnerId,
                            NotificationEvents.JourneyFavorited,
                            new { journeyId = evt.JourneyId, favoritedByUserId = evt.UserId },
                            ct);
                    }

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

                    // Notify both favoriters and shared recipients
                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        NotificationEvents.JourneyUpdated,
                        new { evt.JourneyId },
                        ct);

                    await NotifySharedRecipientsAsync(
                        evt.JourneyId,
                        NotificationEvents.JourneyUpdated,
                        new { evt.JourneyId },
                        ct);

                    break;
                }

            case nameof(JourneyDeleted):
                {
                    var evt = JsonSerializer.Deserialize<JourneyDeleted>(payloadJson)!;

                    // Delete all journey_favourites entries for this journeyId
                    var favourites = await _db.JourneyFavourites
                        .Where(x => x.JourneyId == evt.JourneyId)
                        .ToListAsync(ct);
                    _db.JourneyFavourites.RemoveRange(favourites);

                    // Delete all journey_shares entries for this journeyId
                    var shares = await _db.Set<Persistence.JourneyShare>()
                        .Where(x => x.JourneyId == evt.JourneyId)
                        .ToListAsync(ct);
                    _db.Set<Persistence.JourneyShare>().RemoveRange(shares);

                    // Notify both favoriters and shared recipients
                    await NotifyFavouritersAsync(
                        evt.JourneyId,
                        NotificationEvents.JourneyDeleted,
                        new { evt.JourneyId },
                        ct);

                    await NotifySharedRecipientsAsync(
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
                        // Track the share locally
                        var existingShare = await _db.Set<Persistence.JourneyShare>()
                            .FindAsync([evt.JourneyId, evt.SharedWithUserId.Value], ct);
                        
                        if (existingShare == null)
                        {
                            _db.Set<Persistence.JourneyShare>().Add(new Persistence.JourneyShare
                            {
                                JourneyId = evt.JourneyId,
                                SharedWithUserId = evt.SharedWithUserId.Value,
                                SharedAtUtc = DateTime.UtcNow
                            });
                        }

                        await NotifyUserAsync(
                            evt.SharedWithUserId.Value,
                            NotificationEvents.JourneyShared,
                            new { evt.JourneyId },
                            ct);
                    }

                    break;
                }

            case nameof(JourneyUnshared):
                {
                    var evt = JsonSerializer.Deserialize<JourneyUnshared>(payloadJson)!;

                    // Only notify if there's a specific user who was unshared (not public link revocation)
                    if (evt.UnsharedFromUserId.HasValue)
                    {
                        // Remove the share from local tracking
                        var share = await _db.Set<Persistence.JourneyShare>()
                            .FindAsync([evt.JourneyId, evt.UnsharedFromUserId.Value], ct);
                        
                        if (share != null)
                        {
                            _db.Set<Persistence.JourneyShare>().Remove(share);
                        }

                        await NotifyUserAsync(
                            evt.UnsharedFromUserId.Value,
                            NotificationEvents.JourneyUnshared,
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
        // Always try SignalR first - it will only send to connected clients
        // SignalR will silently ignore if user is not connected (no exception thrown)
        // We check presence to decide if we should also send email as backup
        var isOnline = _presence.IsOnline(userId);
        _logger.LogInformation("Notifying user {UserId} of event {EventType}, online: {IsOnline}", userId, eventType, isOnline);

        try
        {
            await _notifier.NotifyAsync(userId, eventType, payload);
            _logger.LogDebug("SignalR notification sent to user {UserId}, event: {EventType}", userId, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR notification to user {UserId}, event: {EventType}", userId, eventType);
            // Continue to email fallback
        }

        // Send email as backup if user is not online
        if (!isOnline)
        {
            try
            {
                await _email.SendAsync(
                    userId,
                    "Journey notification",
                    $"Event: {eventType}");
                _logger.LogDebug("Email notification sent to user {UserId}, event: {EventType}", userId, eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notification to user {UserId}, event: {EventType}", userId, eventType);
            }
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
            await NotifyUserAsync(userId, eventType, payload, ct);
        }
    }

    private async Task NotifySharedRecipientsAsync(
        Guid journeyId,
        string eventType,
        object payload,
        CancellationToken ct)
    {
        var recipients = await _db.Set<Persistence.JourneyShare>()
            .Where(x => x.JourneyId == journeyId)
            .Select(x => x.SharedWithUserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in recipients)
        {
            await NotifyUserAsync(userId, eventType, payload, ct);
        }
    }
}

