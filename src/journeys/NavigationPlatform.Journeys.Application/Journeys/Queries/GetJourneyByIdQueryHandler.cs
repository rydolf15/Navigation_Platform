using MediatR;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Application.Journeys.Dtos;

namespace NavigationPlatform.Application.Journeys.Queries;

public sealed class GetJourneyByIdQueryHandler
    : IRequestHandler<GetJourneyByIdQuery, JourneyDto?>
{
    private readonly IJourneyReadRepository _readRepository;
    private readonly ICurrentUser _currentUser;

    public GetJourneyByIdQueryHandler(
        IJourneyReadRepository readRepository,
        ICurrentUser currentUser)
    {
        _readRepository = readRepository;
        _currentUser = currentUser;
    }

    public Task<JourneyDto?> Handle(
        GetJourneyByIdQuery request,
        CancellationToken ct)
    {
        return _readRepository.GetByIdAsync(
            request.Id,
            _currentUser.UserId,
            ct);
    }
}