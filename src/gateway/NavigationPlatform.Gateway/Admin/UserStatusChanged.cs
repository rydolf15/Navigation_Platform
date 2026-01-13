namespace NavigationPlatform.Gateway.Admin;

public sealed record UserStatusChanged(
    Guid UserId,
    string Status,
    Guid ChangedByUserId,
    DateTime OccurredUtc);

