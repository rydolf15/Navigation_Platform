namespace NavigationPlatform.Infrastructure.Persistence.Sharing;

public sealed class JourneyPublicLink
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JourneyId { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? RevokedUtc { get; set; }
}