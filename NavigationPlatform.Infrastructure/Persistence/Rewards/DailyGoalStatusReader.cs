using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Application.Abstractions.Rewards;
using NavigationPlatform.Application.Journeys.Dtos;
using NavigationPlatform.Infrastructure.Persistence.Rewards;

namespace NavigationPlatform.Infrastructure.Rewards;

public sealed class DailyGoalStatusReader : IDailyGoalStatusReader
{
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

        return row is null
            ? new DailyGoalStatusDto(false, null, null)
            : new DailyGoalStatusDto(row.RewardGranted, row.Date, row.TotalDistanceKm);
    }
}
