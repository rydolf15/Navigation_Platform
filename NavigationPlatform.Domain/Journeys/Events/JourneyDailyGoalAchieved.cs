namespace NavigationPlatform.Domain.Journeys.Events;

public sealed record JourneyDailyGoalAchieved(
    Guid UserId,
    DateOnly Date,
    decimal TotalDistanceKm
);
