using System.Text.Json;

namespace NavigationPlatform.RewardWorker.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTime OccurredUtc { get; init; }
    public bool Processed { get; set; }

    public static OutboxMessage From(object evt)
        => new()
        {
            Type = evt.GetType().Name,
            Payload = JsonSerializer.Serialize(evt),
            OccurredUtc = DateTime.UtcNow
        };
}

