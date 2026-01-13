using System.Text.Json;

namespace NavigationPlatform.Gateway.Persistence;

public sealed class GatewayOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public bool Processed { get; set; }

    public static GatewayOutboxMessage From(object evt)
        => new()
        {
            Type = evt.GetType().Name,
            Payload = JsonSerializer.Serialize(evt),
            OccurredUtc = DateTime.UtcNow,
            Processed = false
        };
}

