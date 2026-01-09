using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyUpdated(Guid JourneyId, Guid UserId)
    : DomainEvent(Guid.NewGuid(), DateTime.UtcNow),
        IJourneyEvent;