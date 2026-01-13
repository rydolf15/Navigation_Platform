using NavigationPlatform.Domain.Common;

namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyDailyGoalAchieved(
    Guid JourneyId,
    Guid UserId,
    DateOnly Date,
    decimal TotalDistanceKm
) : DomainEvent(Guid.NewGuid(), DateTime.UtcNow);
