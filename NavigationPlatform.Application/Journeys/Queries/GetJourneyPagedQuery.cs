using MediatR;
using NavigationPlatform.Application.Common.Paging;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Journeys.Queries;

public sealed record GetJourneysPagedQuery(
    int Page,
    int PageSize
) : IRequest<PagedResult<JourneyDto>>;