using MediatR;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Domain.Journeys;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed class UpdateJourneyCommandHandler
    : IRequestHandler<UpdateJourneyCommand>
{
    private readonly IJourneyRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateJourneyCommandHandler(
        IJourneyRepository repo,
        IUnitOfWork uow,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateJourneyCommand request, CancellationToken ct)
    {
        var journey = await _repo.GetByIdAsync(request.JourneyId, ct)
            ?? throw new KeyNotFoundException("Journey not found.");

        if (journey.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("User is not the owner of this journey.");

        // Ensure UTC conversion as safety measure (JSON converter should handle this, but this provides defense in depth)
        var startTime = request.StartTime.Kind switch
        {
            DateTimeKind.Utc => request.StartTime,
            DateTimeKind.Local => request.StartTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc)
        };
        
        var arrivalTime = request.ArrivalTime.Kind switch
        {
            DateTimeKind.Utc => request.ArrivalTime,
            DateTimeKind.Local => request.ArrivalTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(request.ArrivalTime, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(request.ArrivalTime, DateTimeKind.Utc)
        };

        journey.Update(
            request.StartLocation,
            startTime,
            request.ArrivalLocation,
            arrivalTime,
            Enum.Parse<TransportType>(request.TransportType),
            new DistanceKm(request.DistanceKm)
        );

        await _uow.CommitAsync(ct);
    }
}