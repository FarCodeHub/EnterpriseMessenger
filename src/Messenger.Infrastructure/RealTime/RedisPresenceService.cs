using Messenger.Application.Abstractions.Realtime;
using StackExchange.Redis;

namespace Messenger.Infrastructure.Realtime;

public sealed class RedisPresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
private static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(90);
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

public Task JoinConversationAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken)
{
    return RenewConversationPresenceAsync(
        conversationId,
        userId,
        connectionId,
        cancellationToken);
}

 public async Task<bool> LeaveConversationAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken)
{
    var db = _redis.GetDatabase();

    var userConnectionsKey =
        $"presence:conversation:{conversationId}:user:{userId}:connections";

    var conversationUsersKey =
        $"presence:conversation:{conversationId}:users";

    var connectionConversationsKey =
        $"presence:connection:{connectionId}:conversations";

    var currentConnectionLeaseKey =
        $"presence:conversation:{conversationId}:user:{userId}:connection:{connectionId}";

    await db.SetRemoveAsync(userConnectionsKey, connectionId);
    await db.SetRemoveAsync(connectionConversationsKey, conversationId.ToString());
    await db.KeyDeleteAsync(currentConnectionLeaseKey);

    var remainingConversationCount =
        await db.SetLengthAsync(connectionConversationsKey);

    if (remainingConversationCount == 0)
    {
        await db.KeyDeleteAsync(connectionConversationsKey);
    }

    var connections = await db.SetMembersAsync(userConnectionsKey);

    var hasActiveConnection = false;

    foreach (var value in connections)
    {
        var existingConnectionId = value.ToString();

        var leaseKey =
            $"presence:conversation:{conversationId}:user:{userId}:connection:{existingConnectionId}";

        var leaseExists = await db.KeyExistsAsync(leaseKey);

        if (leaseExists)
        {
            hasActiveConnection = true;
            continue;
        }

        await db.SetRemoveAsync(userConnectionsKey, existingConnectionId);
    }

    if (hasActiveConnection)
        return false;

    await db.SetRemoveAsync(conversationUsersKey, userId.ToString());
    await db.KeyDeleteAsync(userConnectionsKey);

    return true;
}
   
   
   
   
   
   
public async Task<IReadOnlyCollection<Guid>> GetOnlineConversationUserIdsAsync(
    Guid conversationId,
    CancellationToken cancellationToken)
{
    var db = _redis.GetDatabase();

    var conversationUsersKey =
        $"presence:conversation:{conversationId}:users";

    var userValues = await db.SetMembersAsync(conversationUsersKey);

    var onlineUserIds = new List<Guid>();

    foreach (var userValue in userValues)
    {
        if (!Guid.TryParse(userValue.ToString(), out var userId))
        {
            await db.SetRemoveAsync(conversationUsersKey, userValue);
            continue;
        }

        var userConnectionsKey =
            $"presence:conversation:{conversationId}:user:{userId}:connections";

        var connectionValues = await db.SetMembersAsync(userConnectionsKey);

        var hasActiveConnection = false;

        foreach (var connectionValue in connectionValues)
        {
            var connectionId = connectionValue.ToString();

            var leaseKey =
                $"presence:conversation:{conversationId}:user:{userId}:connection:{connectionId}";

            var leaseExists = await db.KeyExistsAsync(leaseKey);

            if (leaseExists)
            {
                hasActiveConnection = true;
                continue;
            }

            await db.SetRemoveAsync(userConnectionsKey, connectionId);
        }

        if (hasActiveConnection)
        {
            onlineUserIds.Add(userId);
            continue;
        }

        await db.SetRemoveAsync(conversationUsersKey, userId.ToString());
        await db.KeyDeleteAsync(userConnectionsKey);
    }

    return onlineUserIds;
}



    public async Task<IReadOnlyCollection<Guid>> GetConnectionConversationIdsAsync(
    string connectionId,
    CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        var key = $"presence:connection:{connectionId}:conversations";

        var values = await db.SetMembersAsync(key);

        return values
            .Select(x => Guid.TryParse(x.ToString(), out var conversationId)
                ? conversationId
                : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToList();
    }

public async Task RenewConversationPresenceAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken)
{
    var db = _redis.GetDatabase();

    var conversationConnectionLeaseKey =
        $"presence:conversation:{conversationId}:user:{userId}:connection:{connectionId}";

    var connectionConversationsKey =
        $"presence:connection:{connectionId}:conversations";

    var userConnectionsKey =
        $"presence:conversation:{conversationId}:user:{userId}:connections";

    var conversationUsersKey =
        $"presence:conversation:{conversationId}:users";

    await db.StringSetAsync(
        conversationConnectionLeaseKey,
        "1",
        PresenceTtl);

    await db.SetAddAsync(userConnectionsKey, connectionId);
    await db.SetAddAsync(conversationUsersKey, userId.ToString());
    await db.SetAddAsync(connectionConversationsKey, conversationId.ToString());

    await db.KeyExpireAsync(userConnectionsKey, PresenceTtl);
    await db.KeyExpireAsync(conversationUsersKey, PresenceTtl);
    await db.KeyExpireAsync(connectionConversationsKey, PresenceTtl);
}
}