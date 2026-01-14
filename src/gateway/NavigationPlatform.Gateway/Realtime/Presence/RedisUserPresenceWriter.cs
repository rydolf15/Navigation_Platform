using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace NavigationPlatform.Gateway.Realtime.Presence;

internal sealed class RedisUserPresenceWriter : IUserPresenceWriter
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisUserPresenceWriter>? _logger;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public RedisUserPresenceWriter(IConnectionMultiplexer redis, ILogger<RedisUserPresenceWriter>? logger = null)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task SetOnlineAsync(Guid userId)
    {
        var key = $"presence:{userId}";
        _logger?.LogInformation("Setting presence key {Key} in Redis with TTL {Ttl}", key, Ttl);
        
        var result = await _db.StringSetAsync(key, "1", Ttl);
        _logger?.LogInformation("Presence key {Key} set result: {Result}", key, result);
        
        // Verify it was set
        var exists = await _db.KeyExistsAsync(key);
        _logger?.LogInformation("Presence key {Key} exists after set: {Exists}", key, exists);
    }

    public async Task SetOfflineAsync(Guid userId)
    {
        var key = $"presence:{userId}";
        _logger?.LogInformation("Removing presence key {Key} from Redis", key);
        
        var result = await _db.KeyDeleteAsync(key);
        _logger?.LogInformation("Presence key {Key} delete result: {Result}", key, result);
    }
}

