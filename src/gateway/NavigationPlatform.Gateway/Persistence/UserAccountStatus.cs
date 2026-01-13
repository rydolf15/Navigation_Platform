namespace NavigationPlatform.Gateway.Persistence;

public sealed class UserAccountStatus
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

