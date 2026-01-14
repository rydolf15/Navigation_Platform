using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Domain.Journeys.Events;

namespace NavigationPlatform.Infrastructure.Persistence.Rewards;

public sealed class DailyGoalAchievedHandler
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly RewardReadDbContext _rewardsDb;

    public DailyGoalAchievedHandler(
        AppDbContext db,
        IUnitOfWork uow,
        RewardReadDbContext rewardsDb)
    {
        _db = db;
        _uow = uow;
        _rewardsDb = rewardsDb;
    }

    public async Task HandleAsync(JourneyDailyGoalAchieved evt, CancellationToken ct)
    {
        var journey = await _db.Journeys.FindAsync([evt.JourneyId], ct);
        if (journey != null)
        {
            journey.MarkDailyGoalAchieved();
            await _uow.CommitAsync(ct);
        }

        var row = await _rewardsDb.DailyDistances.FindAsync([evt.UserId, evt.Date], ct);
        if (row == null)
        {
            _rewardsDb.DailyDistances.Add(new DailyDistanceProjection
            {
                UserId = evt.UserId,
                Date = evt.Date,
                TotalDistanceKm = evt.TotalDistanceKm,
                RewardGranted = true
            });
        }
        else
        {
            row.TotalDistanceKm = evt.TotalDistanceKm;
            row.RewardGranted = true;
        }

        await _rewardsDb.SaveChangesAsync(ct);
    }
}

