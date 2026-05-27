using Messenger.Application.Abstractions.Realtime;
using StackExchange.Redis;

namespace Messenger.Infrastructure.Realtime;

public sealed class RedisPresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisPresenceService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task SetOnlineAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        var key = $"presence:{userId}";

        await db.SetAddAsync(key, connectionId);

        await db.KeyExpireAsync(key, TimeSpan.FromHours(12));
    }

    public async Task<bool> RemoveConnectionAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        var key = $"presence:{userId}";

        await db.SetRemoveAsync(key, connectionId);

        var count = await db.SetLengthAsync(key);

        return count == 0;
    }

    public async Task<bool> IsOnlineAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        var key = $"presence:{userId}";

        return await db.KeyExistsAsync(key);
    }


    
}