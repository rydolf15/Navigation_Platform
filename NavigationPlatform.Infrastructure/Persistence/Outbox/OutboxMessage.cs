using System.Text.Json;

namespace NavigationPlatform.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTime OccurredUtc { get; init; }
    public bool Processed { get; set; }

    public static OutboxMessage From(object domainEvent)
        => new()
        {
            Type = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent),
            OccurredUtc = DateTime.UtcNow
        };
}