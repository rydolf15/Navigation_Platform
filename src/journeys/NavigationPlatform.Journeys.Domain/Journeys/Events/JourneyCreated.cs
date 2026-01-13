using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyCreated(
    Guid JourneyId,
    Guid UserId,
    DateTime StartTime,
    decimal DistanceKm)
    : DomainEvent(Guid.NewGuid(), DateTime.UtcNow),
        IJourneyDistanceEvent;