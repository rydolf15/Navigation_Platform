using NavigationPlatform.Application.Common.Paging;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Abstractions.Persistence;

public interface IJourneyReadRepository
{
    Task<PagedResult<JourneyDto>> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct);
}