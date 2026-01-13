using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyUpdated(
    Guid JourneyId,
    Guid UserId,
    DateTime StartTime,
    decimal DistanceKm)
    : DomainEvent(Guid.NewGuid(), DateTime.UtcNow),
        IJourneyEvent,
        IJourneyDistanceEvent;