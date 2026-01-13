namespace NavigationPlatform.Infrastructure.Persistence.Analytics;

public sealed class MonthlyDistanceProjection
{
    public Guid UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalDistanceKm { get; set; }
}

