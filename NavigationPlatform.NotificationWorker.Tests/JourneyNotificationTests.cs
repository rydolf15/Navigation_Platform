using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.NotificationWorker.Messaging;
using NavigationPlatform.NotificationWorker.Persistence;
using NavigationPlatform.NotificationWorker.Processing;
using NavigationPlatform.NotificationWorker.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace NavigationPlatform.NotificationWorker.Tests;

public sealed class JourneyNotificationTests
{
    [Fact]
    public async Task JourneyUpdated_OnlyNotifiesFavoriters_NotSharedRecipients()
    {
        // Arrange
        var journeyId = Guid.NewGuid();
        var favoriterId = Guid.NewGuid();
        var sharedRecipientId = Guid.NewGuid();
        var nonFavoriterId = Guid.NewGuid();

        var db = CreateInMemoryDbContext();
        db.JourneyFavourites.Add(new JourneyFavourite
        {
            JourneyId = journeyId,
            UserId = favoriterId
        });
        db.Set<JourneyShare>().Add(new JourneyShare
        {
            JourneyId = journeyId,
            SharedWithUserId = sharedRecipientId,
            SharedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var signalRNotifier = new FakeSignalRNotifier();
        var emailSender = new FakeEmailSender();
        var presence = new FakeUserPresence();

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalRNotifier,
            emailSender,
            CreateLogger());

        var evt = new JourneyUpdated(
            journeyId,
            Guid.NewGuid(), // owner
            DateTime.UtcNow,
            100m);

        // Act
        await processor.ProcessAsync(
            Guid.NewGuid(),
            nameof(JourneyUpdated),
            System.Text.Json.JsonSerializer.Serialize(evt),
            CancellationToken.None);

        // Assert
        // Only favoriter should receive notification
        Assert.Single(signalRNotifier.Notifications);
        Assert.Equal(favoriterId, signalRNotifier.Notifications[0].UserId);
        Assert.Equal(NotificationEvents.JourneyUpdated, signalRNotifier.Notifications[0].EventType);

        // Shared recipient should NOT receive notification
        Assert.DoesNotContain(signalRNotifier.Notifications, n => n.UserId == sharedRecipientId);
        
        // Non-favoriter should NOT receive notification
        Assert.DoesNotContain(signalRNotifier.Notifications, n => n.UserId == nonFavoriterId);
    }

    [Fact]
    public async Task JourneyDeleted_OnlyNotifiesFavoriters_NotSharedRecipients()
    {
        // Arrange
        var journeyId = Guid.NewGuid();
        var favoriterId = Guid.NewGuid();
        var sharedRecipientId = Guid.NewGuid();
        var nonFavoriterId = Guid.NewGuid();

        var db = CreateInMemoryDbContext();
        db.JourneyFavourites.Add(new JourneyFavourite
        {
            JourneyId = journeyId,
            UserId = favoriterId
        });
        db.Set<JourneyShare>().Add(new JourneyShare
        {
            JourneyId = journeyId,
            SharedWithUserId = sharedRecipientId,
            SharedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var signalRNotifier = new FakeSignalRNotifier();
        var emailSender = new FakeEmailSender();
        var presence = new FakeUserPresence();

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalRNotifier,
            emailSender,
            CreateLogger());

        var evt = new JourneyDeleted(
            journeyId,
            Guid.NewGuid(), // owner
            DateTime.UtcNow,
            100m);

        // Act
        await processor.ProcessAsync(
            Guid.NewGuid(),
            nameof(JourneyDeleted),
            System.Text.Json.JsonSerializer.Serialize(evt),
            CancellationToken.None);

        // Assert
        // Only favoriter should receive notification
        Assert.Single(signalRNotifier.Notifications);
        Assert.Equal(favoriterId, signalRNotifier.Notifications[0].UserId);
        Assert.Equal(NotificationEvents.JourneyDeleted, signalRNotifier.Notifications[0].EventType);

        // Shared recipient should NOT receive notification
        Assert.DoesNotContain(signalRNotifier.Notifications, n => n.UserId == sharedRecipientId);
        
        // Non-favoriter should NOT receive notification
        Assert.DoesNotContain(signalRNotifier.Notifications, n => n.UserId == nonFavoriterId);
    }

    [Fact]
    public async Task JourneyUpdated_OfflineUser_ReceivesEmailFallback()
    {
        // Arrange
        var journeyId = Guid.NewGuid();
        var favoriterId = Guid.NewGuid();

        var db = CreateInMemoryDbContext();
        db.JourneyFavourites.Add(new JourneyFavourite
        {
            JourneyId = journeyId,
            UserId = favoriterId
        });
        await db.SaveChangesAsync();

        var signalRNotifier = new FakeSignalRNotifier();
        var emailSender = new FakeEmailSender();
        var presence = new FakeUserPresence(); // User is offline by default

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalRNotifier,
            emailSender,
            CreateLogger());

        var evt = new JourneyUpdated(
            journeyId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            100m);

        // Act
        await processor.ProcessAsync(
            Guid.NewGuid(),
            nameof(JourneyUpdated),
            System.Text.Json.JsonSerializer.Serialize(evt),
            CancellationToken.None);

        // Assert
        // SignalR should still be attempted
        Assert.Single(signalRNotifier.Notifications);
        Assert.Equal(favoriterId, signalRNotifier.Notifications[0].UserId);

        // Email should be sent as fallback (user is offline)
        Assert.Single(emailSender.Emails);
        Assert.Equal(favoriterId, emailSender.Emails[0].UserId);
    }

    [Fact]
    public async Task JourneyDeleted_OfflineUser_ReceivesEmailFallback()
    {
        // Arrange
        var journeyId = Guid.NewGuid();
        var favoriterId = Guid.NewGuid();

        var db = CreateInMemoryDbContext();
        db.JourneyFavourites.Add(new JourneyFavourite
        {
            JourneyId = journeyId,
            UserId = favoriterId
        });
        await db.SaveChangesAsync();

        var signalRNotifier = new FakeSignalRNotifier();
        var emailSender = new FakeEmailSender();
        var presence = new FakeUserPresence(); // User is offline by default

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalRNotifier,
            emailSender,
            CreateLogger());

        var evt = new JourneyDeleted(
            journeyId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            100m);

        // Act
        await processor.ProcessAsync(
            Guid.NewGuid(),
            nameof(JourneyDeleted),
            System.Text.Json.JsonSerializer.Serialize(evt),
            CancellationToken.None);

        // Assert
        // SignalR should still be attempted
        Assert.Single(signalRNotifier.Notifications);
        Assert.Equal(favoriterId, signalRNotifier.Notifications[0].UserId);

        // Email should be sent as fallback (user is offline)
        Assert.Single(emailSender.Emails);
        Assert.Equal(favoriterId, emailSender.Emails[0].UserId);
    }

    [Fact]
    public async Task JourneyUpdated_OnlineUser_NoEmailSent()
    {
        // Arrange
        var journeyId = Guid.NewGuid();
        var favoriterId = Guid.NewGuid();

        var db = CreateInMemoryDbContext();
        db.JourneyFavourites.Add(new JourneyFavourite
        {
            JourneyId = journeyId,
            UserId = favoriterId
        });
        await db.SaveChangesAsync();

        var signalRNotifier = new FakeSignalRNotifier();
        var emailSender = new FakeEmailSender();
        var presence = new FakeUserPresence();
        presence.SetOnline(favoriterId); // User is online

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalRNotifier,
            emailSender,
            CreateLogger());

        var evt = new JourneyUpdated(
            journeyId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            100m);

        // Act
        await processor.ProcessAsync(
            Guid.NewGuid(),
            nameof(JourneyUpdated),
            System.Text.Json.JsonSerializer.Serialize(evt),
            CancellationToken.None);

        // Assert
        // SignalR notification should be sent
        Assert.Single(signalRNotifier.Notifications);

        // No email should be sent (user is online)
        Assert.Empty(emailSender.Emails);
    }

    [Fact]
    public async Task JourneyUpdated_MultipleFavoriters_AllReceiveNotifications()
    {
        // Arrange
        var journeyId = Guid.NewGuid();
        var favoriter1Id = Guid.NewGuid();
        var favoriter2Id = Guid.NewGuid();
        var favoriter3Id = Guid.NewGuid();

        var db = CreateInMemoryDbContext();
        db.JourneyFavourites.AddRange(new[]
        {
            new JourneyFavourite { JourneyId = journeyId, UserId = favoriter1Id },
            new JourneyFavourite { JourneyId = journeyId, UserId = favoriter2Id },
            new JourneyFavourite { JourneyId = journeyId, UserId = favoriter3Id }
        });
        await db.SaveChangesAsync();

        var signalRNotifier = new FakeSignalRNotifier();
        var emailSender = new FakeEmailSender();
        var presence = new FakeUserPresence();

        var processor = new NotificationEventProcessor(
            db,
            presence,
            signalRNotifier,
            emailSender,
            CreateLogger());

        var evt = new JourneyUpdated(
            journeyId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            100m);

        // Act
        await processor.ProcessAsync(
            Guid.NewGuid(),
            nameof(JourneyUpdated),
            System.Text.Json.JsonSerializer.Serialize(evt),
            CancellationToken.None);

        // Assert
        // All favoriters should receive notifications
        Assert.Equal(3, signalRNotifier.Notifications.Count);
        Assert.Contains(signalRNotifier.Notifications, n => n.UserId == favoriter1Id);
        Assert.Contains(signalRNotifier.Notifications, n => n.UserId == favoriter2Id);
        Assert.Contains(signalRNotifier.Notifications, n => n.UserId == favoriter3Id);
    }

    private static NotificationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new NotificationDbContext(options);
        return db;
    }

    private static ILogger<NotificationEventProcessor> CreateLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        return loggerFactory.CreateLogger<NotificationEventProcessor>();
    }
}
