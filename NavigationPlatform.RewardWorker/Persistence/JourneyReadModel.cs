namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class JourneyReadModel
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTime StartTime { get; init; }
    public decimal DistanceKm { get; init; }
}
