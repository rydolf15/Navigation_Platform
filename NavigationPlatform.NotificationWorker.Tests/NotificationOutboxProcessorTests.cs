using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;
using NavigationPlatform.NotificationWorker.Persistence;
using NavigationPlatform.NotificationWorker.Processing;
using NavigationPlatform.NotificationWorker.Tests.Fakes;
using System.Text.Json;

namespace NavigationPlatform.NotificationWorker.Tests;

public sealed class NotificationOutboxProcessorTests
{
    [Fact]
    public async Task Only_favouriting_users_receive_notifications()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var journeyId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase("notif-db")
            .Options;

        using var db = new NotificationDbContext(options);

        db.JourneyFavourites.Add(new JourneyFavourite { JourneyId = journeyId, UserId = userA });

        var evt = new JourneyUpdated(journeyId, Guid.NewGuid());

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(JourneyUpdated),
            Payload = JsonSerializer.Serialize(
                new JourneyUpdated(journeyId, userA)),
            OccurredUtc = DateTime.UtcNow,
            Processed = false
        });

        await db.SaveChangesAsync();

        var presence = new FakeUserPresence();
        presence.SetOnline(userA);

        var signalr = new FakeSignalRNotifier();
        var email = new FakeEmailSender();

        var processor = new NotificationOutboxProcessor(
            db,
            presence,
            signalr,
            email);

        await processor.ProcessAsync(CancellationToken.None);

        Assert.Contains(userA, signalr.NotifiedUsers);
        Assert.DoesNotContain(userB, signalr.NotifiedUsers);
        Assert.Empty(email.EmailsSent);
    }
}
