using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyFavorited(Guid JourneyId, Guid UserId, Guid JourneyOwnerId)
    : DomainEvent(Guid.NewGuid(), DateTime.UtcNow),
        IJourneyEvent;
