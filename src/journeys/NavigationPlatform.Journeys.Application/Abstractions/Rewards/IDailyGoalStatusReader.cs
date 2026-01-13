using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Abstractions.Rewards;

public interface IDailyGoalStatusReader
{
    Task<DailyGoalStatusDto> GetTodayAsync(Guid userId, CancellationToken ct);
}
