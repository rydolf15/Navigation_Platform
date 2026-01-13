namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class InboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public DateTime OccurredUtc { get; set; }
    public DateTime ProcessedUtc { get; set; }
}

