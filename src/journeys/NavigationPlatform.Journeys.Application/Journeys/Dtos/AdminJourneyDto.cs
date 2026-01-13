namespace NavigationPlatform.Application.Journeys.Dtos;

public sealed record AdminJourneyDto(
    Guid Id,
    Guid UserId,
    string StartLocation,
    DateTime StartTime,
    string ArrivalLocation,
    DateTime ArrivalTime,
    string TransportType,
    decimal DistanceKm,
    bool IsDailyGoalAchieved);

