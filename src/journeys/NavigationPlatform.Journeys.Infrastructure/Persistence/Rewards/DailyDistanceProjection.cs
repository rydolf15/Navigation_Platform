namespace NavigationPlatform.Infrastructure.Persistence.Rewards;

public sealed class DailyDistanceProjection
{
    public Guid UserId { get; init; }
    public DateOnly Date { get; init; }
    public decimal TotalDistanceKm { get; set; }
    public bool RewardGranted { get; set; }
}