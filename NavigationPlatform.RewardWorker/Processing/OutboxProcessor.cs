using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.Infrastructure.Persistence.Outbox;
using NavigationPlatform.Infrastructure.Persistence.Rewards;
using NavigationPlatform.RewardWorker.Persistence;
using NavigationPlatform.RewardWorker.Processing;
using System.Text.Json;

internal sealed class OutboxProcessor
{
    private const decimal DailyThreshold = 20.00m;
    private readonly RewardDbContext _db;

    public OutboxProcessor(RewardDbContext db)
    {
        _db = db;
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        var messages = await _db.OutboxMessages
            .Where(x => !x.Processed)
            .OrderBy(x => x.OccurredUtc)
            .Take(100)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            if (msg.Type is nameof(JourneyCreated) or nameof(JourneyUpdated))
            {
                await HandleJourneyEvent(msg, ct);
            }

            msg.Processed = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleJourneyEvent(OutboxMessage msg, CancellationToken ct)
    {
        object evt = msg.Type switch
        {
            nameof(JourneyCreated) =>
                JsonSerializer.Deserialize<JourneyCreated>(msg.Payload)!,

            nameof(JourneyUpdated) =>
                JsonSerializer.Deserialize<JourneyUpdated>(msg.Payload)!,

            _ => throw new InvalidOperationException(
                $"Unsupported event type: {msg.Type}")
        };

        Guid journeyId = evt switch
        {
            JourneyCreated e => e.JourneyId,
            JourneyUpdated e => e.JourneyId,
            _ => throw new InvalidOperationException()
        };

        var journey = await LoadJourneyAsync(journeyId, ct);

        var journeyDate = DateOnly.FromDateTime(journey.StartTime);

        var projection = await _db.Projections.FindAsync(
            [journey.UserId, journeyDate], ct);

        if (projection == null)
        {
            projection = new DailyDistanceProjection
            {
                UserId = journey.UserId,
                Date = journeyDate,
                TotalDistanceKm = 0,
                RewardGranted = false
            };
            _db.Projections.Add(projection);
        }

        projection.TotalDistanceKm += journey.DistanceKm;

        if (!projection.RewardGranted &&
            DailyRewardEvaluator.ShouldGrant(projection.TotalDistanceKm))
        {
            projection.RewardGranted = true;

            _db.OutboxMessages.Add(
                OutboxMessage.From(
                    new JourneyDailyGoalAchieved(
                        journey.UserId,
                        journeyDate,
                        projection.TotalDistanceKm)));

            await MarkJourneyAsAchievedAsync(journey.Id, ct);
        }
    }


    private async Task PublishDailyGoalAchievedAsync(Guid userId, DateOnly date, decimal totalKm, CancellationToken ct)
    {
        _db.OutboxMessages.Add(
            OutboxMessage.From(
                new JourneyDailyGoalAchieved(
                    userId,
                    date,
                    totalKm)));
    }

    private async Task MarkJourneyAsAchievedAsync(Guid journeyId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE journeys
            SET is_daily_goal_achieved = true
            WHERE id = {journeyId}
        """, ct);
    }

    private async Task<JourneyReadModel> LoadJourneyAsync(
    Guid journeyId,
    CancellationToken ct)
    {
        return await _db.Database
            .SqlQuery<JourneyReadModel>($"""
            SELECT
                id,
                user_id AS UserId,
                start_time AS StartTime,
                distance_km AS DistanceKm
            FROM journeys
            WHERE id = {journeyId}
        """)
            .SingleAsync(ct);
    }
}
