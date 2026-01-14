namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class JourneyShare
{
    public Guid JourneyId { get; init; }
    public Guid SharedWithUserId { get; init; }
    public DateTime SharedAtUtc { get; init; }
}
