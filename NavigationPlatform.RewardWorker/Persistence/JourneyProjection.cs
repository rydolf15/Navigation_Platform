namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class JourneyProjection
{
    public Guid JourneyId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal DistanceKm { get; set; }
}

