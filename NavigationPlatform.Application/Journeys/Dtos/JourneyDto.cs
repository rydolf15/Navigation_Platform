namespace NavigationPlatform.Application.Journeys.Dtos;

public sealed record JourneyDto
(
    Guid Id,
    string StartLocation,
    DateTime StartTime,
    string ArrivalLocation,
    DateTime ArrivalTime,
    string TransportType,
    decimal DistanceKm,
    bool IsDailyGoalAchieved
);