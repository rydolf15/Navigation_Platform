using StackExchange.Redis;

namespace NavigationPlatform.NotificationWorker.Messaging;

internal sealed class RedisUserPresence : IUserPresence
{
    private readonly IDatabase _db;

    public RedisUserPresence(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public bool IsOnline(Guid userId)
        => _db.KeyExists($"presence:{userId}");
}