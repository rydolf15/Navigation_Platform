namespace NavigationPlatform.NotificationWorker.Domain;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTime OccurredUtc { get; init; }
    public DateTime? ProcessedUtc { get; set; }
}

