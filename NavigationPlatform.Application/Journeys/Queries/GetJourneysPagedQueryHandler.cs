using MediatR;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Application.Common.Paging;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Journeys.Queries;

public sealed class GetJourneysPagedQueryHandler
    : IRequestHandler<GetJourneysPagedQuery, PagedResult<JourneyDto>>
{
    private readonly IJourneyReadRepository _readRepository;
    private readonly ICurrentUser _currentUser;

    public GetJourneysPagedQueryHandler(
        IJourneyReadRepository readRepository,
        ICurrentUser currentUser)
    {
        _readRepository = readRepository;
        _currentUser = currentUser;
    }

    public Task<PagedResult<JourneyDto>> Handle(
        GetJourneysPagedQuery request,
        CancellationToken ct)
    {
        return _readRepository.GetPagedAsync(
            _currentUser.UserId,
            request.Page,
            request.PageSize,
            ct);
    }
}
