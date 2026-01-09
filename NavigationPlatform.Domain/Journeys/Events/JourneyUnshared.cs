using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyUnshared(
    Guid JourneyId,
    Guid RevokedByUserId,
    Guid? PublicLinkId)
    : DomainEvent(Guid.NewGuid(), DateTime.UtcNow);
