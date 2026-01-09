namespace NavigationPlatform.Infrastructure.Persistence.Audit;

public sealed class JourneyShareAudit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JourneyId { get; init; }
    public Guid ActorUserId { get; init; }
    public string Action { get; init; } = null!;
    public DateTime OccurredUtc { get; init; }
}
