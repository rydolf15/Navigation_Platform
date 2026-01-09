namespace NavigationPlatform.Domain.Journeys.Events;

public interface IJourneyEvent
{
    Guid JourneyId { get; }
    Guid UserId { get; }
}
