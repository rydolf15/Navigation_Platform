using MediatR;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Rewards;
using NavigationPlatform.Application.Journeys.Dtos;
using NavigationPlatform.Application.Journeys.Queries;

namespace NavigationPlatform.Application.Users.Queries;

internal sealed class GetDailyGoalStatusQueryHandler
    : IRequestHandler<GetDailyGoalStatusQuery, DailyGoalStatusDto>
{
    private readonly IDailyGoalStatusReader _reader;
    private readonly ICurrentUser _currentUser;

    public GetDailyGoalStatusQueryHandler(
        IDailyGoalStatusReader reader,
        ICurrentUser currentUser)
    {
        _reader = reader;
        _currentUser = currentUser;
    }

    public Task<DailyGoalStatusDto> Handle(
        GetDailyGoalStatusQuery request,
        CancellationToken ct)
        => _reader.GetTodayAsync(_currentUser.UserId, ct);
}
