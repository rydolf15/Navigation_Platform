using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
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
        await db.SaveChangesAsync();

        var presence = new FakeUserPresence();
        presence.SetOnline(userA);

        var signalr = new FakeSignalRNotifier();
        var email = new FakeEmailSender();

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalr,
            email);

        var msgId = Guid.NewGuid();
        var evt = new JourneyUpdated(journeyId, userA, DateTime.UtcNow, 1.00m);

        await processor.ProcessAsync(
            msgId,
            nameof(JourneyUpdated),
            JsonSerializer.Serialize(evt),
            CancellationToken.None);

        Assert.Contains(userA, signalr.NotifiedUsers);
        Assert.DoesNotContain(userB, signalr.NotifiedUsers);
        Assert.Empty(email.EmailsSent);
    }
}
