using StackExchange.Redis;

namespace NavigationPlatform.Api.Realtime;

internal sealed class RedisUserPresenceWriter : IUserPresenceWriter
{
    private readonly IDatabase _db;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public RedisUserPresenceWriter(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public Task SetOnlineAsync(Guid userId)
        => _db.StringSetAsync($"presence:{userId}", "1", Ttl);

    public Task SetOfflineAsync(Guid userId)
        => _db.KeyDeleteAsync($"presence:{userId}");
}
