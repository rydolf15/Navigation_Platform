using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyShared(
    Guid JourneyId,
    Guid SharedByUserId,
    Guid? SharedWithUserId,
    Guid? PublicLinkId)
    : DomainEvent(Guid.NewGuid(), DateTime.UtcNow);
