namespace NavigationPlatform.Gateway.Persistence;

public sealed class UserStatusAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = null!;
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
}

