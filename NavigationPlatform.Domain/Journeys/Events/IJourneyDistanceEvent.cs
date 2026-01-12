namespace NavigationPlatform.Domain.Journeys.Events;

public interface IJourneyDistanceEvent
{
    Guid JourneyId { get; }
    Guid UserId { get; }
    DateTime StartTime { get; }
    decimal DistanceKm { get; }
}
