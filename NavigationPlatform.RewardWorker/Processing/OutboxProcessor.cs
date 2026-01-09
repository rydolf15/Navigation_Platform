using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Journeys.Events;
using NavigationPlatform.RewardWorker.Persistence;
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
            if (msg.Type == nameof(JourneyCreated) ||
                msg.Type == nameof(JourneyUpdated))
            {
                await HandleJourneyEvent(msg, ct);
            }

            msg.Processed = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleJourneyEvent(
        NavigationPlatform.Infrastructure.Persistence.Outbox.OutboxMessage msg,
        CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<JourneyCreated>(msg.Payload)!;

        var date = DateOnly.FromDateTime(msg.OccurredUtc);
        var projection = await _db.Projections.FindAsync(
            [evt.UserId, date], ct);

        if (projection == null)
        {
            projection = new DailyDistanceProjection
            {
                UserId = evt.UserId,
                Date = date
            };
            _db.Projections.Add(projection);
        }

        projection.TotalDistanceKm += 1; // replaced later by actual distance

        if (!projection.RewardGranted &&
            projection.TotalDistanceKm >= DailyThreshold)
        {
            projection.RewardGranted = true;
            // publish DailyGoalAchieved event later
        }
    }
}
