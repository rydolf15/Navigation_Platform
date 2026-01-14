using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Rewards;
using NavigationPlatform.Application.Journeys.Dtos;
using NavigationPlatform.Infrastructure.Persistence.Rewards;

namespace NavigationPlatform.Infrastructure.Rewards;

public sealed class DailyGoalStatusReader : IDailyGoalStatusReader
{
    private const decimal DailyGoalThresholdKm = 20.00m;
    private readonly RewardReadDbContext _db;

    public DailyGoalStatusReader(RewardReadDbContext db)
    {
        _db = db;
    }

    public async Task<DailyGoalStatusDto> GetTodayAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var row = await _db.DailyDistances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == today, ct);

        if (row is null)
            return new DailyGoalStatusDto(false, null, null);

        // Check if current total distance meets the threshold, not just if reward was granted
        var achieved = row.TotalDistanceKm >= DailyGoalThresholdKm;
        
        return new DailyGoalStatusDto(achieved, row.Date, row.TotalDistanceKm);
    }
}
