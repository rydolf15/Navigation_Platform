namespace NavigationPlatform.Infrastructure.Persistence.Analytics;

public sealed class AnalyticsInboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public DateTime OccurredUtc { get; set; }
    public DateTime ProcessedUtc { get; set; }
}

