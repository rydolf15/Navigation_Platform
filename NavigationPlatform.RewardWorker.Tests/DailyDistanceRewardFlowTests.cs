using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Rewards;
using NavigationPlatform.RewardWorker.Persistence;
using NavigationPlatform.RewardWorker.Processing;
using System.Text.Json;

namespace NavigationPlatform.RewardWorker.Tests;

public sealed class DailyDistanceRewardFlowTests
{
    [Fact]
    public async Task A_B_C_then_edit_B_grants_once_keeps_trigger_and_marks_journey_C()
    {
        var userId = Guid.NewGuid();
        var dayStartUtc = new DateTime(2026, 01, 05, 8, 0, 0, DateTimeKind.Utc);
        var date = DateOnly.FromDateTime(dayStartUtc);

        // Journeys (same user, same calendar day)
        var journeyA = Journey.Create(
            userId,
            "A",
            dayStartUtc.AddHours(1),
            "A2",
            dayStartUtc.AddHours(2),
            TransportType.Car,
            new DistanceKm(5.00m)); // < 20

        var journeyB = Journey.Create(
            userId,
            "B",
            dayStartUtc.AddHours(3),
            "B2",
            dayStartUtc.AddHours(4),
            TransportType.Car,
            new DistanceKm(14.00m)); // < 20

        var journeyC = Journey.Create(
            userId,
            "C",
            dayStartUtc.AddHours(5),
            "C2",
            dayStartUtc.AddHours(6),
            TransportType.Car,
            new DistanceKm(20.01m)); // > 20

        // Reward worker DB
        var rewardDbOptions = new DbContextOptionsBuilder<RewardDbContext>()
            .UseInMemoryDatabase($"rewards-{Guid.NewGuid()}")
            .Options;

        await using var rewardDb = new RewardDbContext(rewardDbOptions);
        var processor = new DailyDistanceRewardProcessor(rewardDb);

        // Apply A, B, C creates in reward worker
        await processor.UpsertAsync(new JourneyCreated(journeyA.Id, userId, journeyA.StartTime, journeyA.DistanceKm));
        await rewardDb.SaveChangesAsync();

        await processor.UpsertAsync(new JourneyCreated(journeyB.Id, userId, journeyB.StartTime, journeyB.DistanceKm));
        await rewardDb.SaveChangesAsync();

        var resultAfterC = await processor.UpsertAsync(new JourneyCreated(journeyC.Id, userId, journeyC.StartTime, journeyC.DistanceKm));
        await rewardDb.SaveChangesAsync();

        // After C: daily goal achieved, reward row exists, outbox event published once.
        resultAfterC.IsGoalAchieved.Should().BeTrue();
        resultAfterC.Date.Should().Be(date);
        resultAfterC.GrantedByJourneyId.Should().Be(journeyC.Id);

        var daily = await rewardDb.DailyDistances.FindAsync(userId, date);
        daily.Should().NotBeNull();
        daily!.RewardGranted.Should().BeTrue();
        daily.GrantedByJourneyId.Should().Be(journeyC.Id);
        var totalAfterC = daily.TotalDistanceKm;

        rewardDb.OutboxMessages
            .Count(x => x.Type == nameof(JourneyDailyGoalAchieved))
            .Should()
            .Be(1);

        var outbox = rewardDb.OutboxMessages.Single(x => x.Type == nameof(JourneyDailyGoalAchieved));
        var publishedEvt = JsonSerializer.Deserialize<JourneyDailyGoalAchieved>(outbox.Payload);
        publishedEvt.Should().NotBeNull();
        publishedEvt!.JourneyId.Should().Be(journeyC.Id);
        publishedEvt.UserId.Should().Be(userId);
        publishedEvt.Date.Should().Be(date);

        // Journey service DB (mark journey C + upsert reward read model)
        var journeysDbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"journeys-{Guid.NewGuid()}")
            .Options;

        var rewardsReadDbOptions = new DbContextOptionsBuilder<RewardReadDbContext>()
            .UseInMemoryDatabase($"rewards-read-{Guid.NewGuid()}")
            .Options;

        await using var journeysDb = new AppDbContext(journeysDbOptions);
        await using var rewardsReadDb = new RewardReadDbContext(rewardsReadDbOptions);

        journeysDb.Journeys.AddRange(journeyA, journeyB, journeyC);
        await journeysDb.SaveChangesAsync();

        var handler = new DailyGoalAchievedHandler(
            journeysDb,
            new FakeUnitOfWork(journeysDb),
            rewardsReadDb);

        await handler.HandleAsync(publishedEvt, CancellationToken.None);

        var reloadedC = await journeysDb.Journeys.AsNoTracking().SingleAsync(x => x.Id == journeyC.Id);
        reloadedC.IsDailyGoalAchieved.Should().BeTrue();

        var readRow = await rewardsReadDb.DailyDistances.FindAsync(userId, date);
        readRow.Should().NotBeNull();
        readRow!.RewardGranted.Should().BeTrue();

        // Edit journey B: bump distance so total would exceed 20 even without C.
        var updatedBKm = 15.00m;
        var resultAfterBUpdate = await processor.UpsertAsync(new JourneyUpdated(journeyB.Id, userId, journeyB.StartTime, updatedBKm));
        await rewardDb.SaveChangesAsync();

        resultAfterBUpdate.IsGoalAchieved.Should().BeTrue();
        resultAfterBUpdate.GrantedByJourneyId.Should().Be(journeyC.Id); // keep original trigger

        var dailyAfterBUpdate = await rewardDb.DailyDistances.FindAsync(userId, date);
        dailyAfterBUpdate.Should().NotBeNull();
        dailyAfterBUpdate!.TotalDistanceKm.Should().Be(totalAfterC + (updatedBKm - 14.00m));

        // No new outbox publication (idempotent reward-per-day)
        rewardDb.OutboxMessages
            .Count(x => x.Type == nameof(JourneyDailyGoalAchieved))
            .Should()
            .Be(1);

        // Still only one daily projection row per day for the user (upsert semantics).
        rewardDb.DailyDistances.Count(x => x.UserId == userId && x.Date == date).Should().Be(1);

        // Re-processing the same triggering event should not duplicate reward.
        await processor.UpsertAsync(new JourneyCreated(journeyC.Id, userId, journeyC.StartTime, journeyC.DistanceKm));
        await rewardDb.SaveChangesAsync();

        rewardDb.OutboxMessages.Count(x => x.Type == nameof(JourneyDailyGoalAchieved)).Should().Be(1);
        rewardDb.DailyDistances.Count(x => x.UserId == userId && x.Date == date).Should().Be(1);
    }

    [Theory]
    // Use hundredths to avoid double->decimal precision issues in InlineData.
    [InlineData(1999, false)]
    [InlineData(2000, true)]
    [InlineData(2001, true)]
    public async Task Processor_threshold_edges_publish_event_correctly(
        int totalHundredths,
        bool expectedGoalAchieved)
    {
        var userId = Guid.NewGuid();
        var dayStartUtc = new DateTime(2026, 01, 06, 8, 0, 0, DateTimeKind.Utc);
        var date = DateOnly.FromDateTime(dayStartUtc);
        var total = totalHundredths / 100m;

        var journey1Id = Guid.NewGuid();
        var journey2Id = Guid.NewGuid();

        // Split total into two journeys to validate accumulation behavior.
        var first = 10.00m;
        var second = decimal.Round(total - first, 2);
        second.Should().BeGreaterThan(0m);

        var rewardDbOptions = new DbContextOptionsBuilder<RewardDbContext>()
            .UseInMemoryDatabase($"rewards-threshold-{Guid.NewGuid()}")
            .Options;

        await using var rewardDb = new RewardDbContext(rewardDbOptions);
        var processor = new DailyDistanceRewardProcessor(rewardDb);

        await processor.UpsertAsync(new JourneyCreated(journey1Id, userId, dayStartUtc.AddHours(1), first));
        await rewardDb.SaveChangesAsync();

        var result = await processor.UpsertAsync(new JourneyCreated(journey2Id, userId, dayStartUtc.AddHours(2), second));
        await rewardDb.SaveChangesAsync();

        result.Date.Should().Be(date);
        result.TotalDistanceKm.Should().Be(total);
        result.IsGoalAchieved.Should().Be(expectedGoalAchieved);

        var daily = await rewardDb.DailyDistances.FindAsync(userId, date);
        daily.Should().NotBeNull();
        daily!.TotalDistanceKm.Should().Be(total);
        daily.RewardGranted.Should().Be(expectedGoalAchieved);

        var outboxCount = rewardDb.OutboxMessages.Count(x => x.Type == nameof(JourneyDailyGoalAchieved));
        outboxCount.Should().Be(expectedGoalAchieved ? 1 : 0);

        if (expectedGoalAchieved)
        {
            daily.GrantedByJourneyId.Should().Be(journey2Id);
            result.GrantedByJourneyId.Should().Be(journey2Id);

            var msg = rewardDb.OutboxMessages.Single(x => x.Type == nameof(JourneyDailyGoalAchieved));
            var evt = JsonSerializer.Deserialize<JourneyDailyGoalAchieved>(msg.Payload);
            evt.Should().NotBeNull();
            evt!.JourneyId.Should().Be(journey2Id);
            evt.UserId.Should().Be(userId);
            evt.Date.Should().Be(date);
            evt.TotalDistanceKm.Should().Be(total);
        }
        else
        {
            daily.GrantedByJourneyId.Should().BeNull();
            result.GrantedByJourneyId.Should().BeNull();
        }
    }

    private sealed class FakeUnitOfWork : NavigationPlatform.Application.Abstractions.Messaging.IUnitOfWork
    {
        private readonly AppDbContext _db;

        public FakeUnitOfWork(AppDbContext db)
        {
            _db = db;
        }

        public Task CommitAsync(CancellationToken ct)
            => _db.SaveChangesAsync(ct);
    }
}

