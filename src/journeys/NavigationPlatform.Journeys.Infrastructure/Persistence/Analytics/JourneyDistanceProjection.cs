namespace NavigationPlatform.Infrastructure.Persistence.Analytics;

public sealed class JourneyDistanceProjection
{
    public Guid JourneyId { get; set; }
    public Guid UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal DistanceKm { get; set; }
    public DateTime StartTime { get; set; }
}

