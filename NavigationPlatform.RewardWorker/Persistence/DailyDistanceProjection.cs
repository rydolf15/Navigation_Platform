namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class DailyDistanceProjection
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }

    public decimal TotalDistanceKm { get; set; }
    public bool RewardGranted { get; set; }

    /// <summary>
    /// The journey that caused the daily threshold to be crossed.
    /// </summary>
    public Guid? GrantedByJourneyId { get; set; }
}

