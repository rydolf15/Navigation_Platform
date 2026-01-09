using MediatR;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Domain.Journeys;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed class CreateJourneyCommandHandler
    : IRequestHandler<CreateJourneyCommand, Guid>
{
    private readonly IJourneyRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateJourneyCommandHandler(
        IJourneyRepository repo,
        IUnitOfWork uow,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateJourneyCommand request, CancellationToken ct)
    {
        var journey = Journey.Create(
            _currentUser.UserId,
            request.StartLocation,
            request.StartTime,
            request.ArrivalLocation,
            request.ArrivalTime,
            Enum.Parse<TransportType>(request.TransportType),
            new DistanceKm(request.DistanceKm));

        await _repo.AddAsync(journey, ct);
        await _uow.CommitAsync(ct);

        return journey.Id;
    }
}
