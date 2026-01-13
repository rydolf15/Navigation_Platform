using MediatR;

namespace NavigationPlatform.Application.Journeys.Commands;

public sealed record CreateJourneyCommand(
    string StartLocation,
    DateTime StartTime,
    string ArrivalLocation,
    DateTime ArrivalTime,
    string TransportType,
    decimal DistanceKm
) : IRequest<Guid>;
