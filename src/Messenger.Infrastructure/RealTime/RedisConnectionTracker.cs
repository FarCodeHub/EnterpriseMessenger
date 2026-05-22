using StackExchange.Redis;
using Messenger.Application.Realtime;
 

namespace Messenger.Infrastructure.Realtime;

public sealed class RedisConnectionTracker : IConnectionTracker
{
    private readonly IDatabase _database;

    public RedisConnectionTracker(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task AddConnectionAsync(Guid userId, string connectionId)
    {
        var userConnectionsKey = GetUserConnectionsKey(userId);
        var connectionUserKey = GetConnectionUserKey(connectionId);
        var onlineUsersKey = GetOnlineUsersKey();

        await _database.SetAddAsync(userConnectionsKey, connectionId);
        await _database.StringSetAsync(connectionUserKey, userId.ToString());
        await _database.SetAddAsync(onlineUsersKey, userId.ToString());
    }

    public async Task<Guid?> RemoveConnectionAsync(string connectionId)
    {
        var connectionUserKey = GetConnectionUserKey(connectionId);

        var userIdValue = await _database.StringGetAsync(connectionUserKey);

        if (userIdValue.IsNullOrEmpty)
            return null;

        var userId = Guid.Parse((string)userIdValue!);

        var userConnectionsKey = GetUserConnectionsKey(userId);

        await _database.SetRemoveAsync(userConnectionsKey, connectionId);
        await _database.KeyDeleteAsync(connectionUserKey);

        var remainingConnections = await _database.SetLengthAsync(userConnectionsKey);

        if (remainingConnections == 0)
        {
            await _database.KeyDeleteAsync(userConnectionsKey);
            await _database.SetRemoveAsync(GetOnlineUsersKey(), userId.ToString());
        }

        return userId;
    }

    public async Task<bool> IsOnlineAsync(Guid userId)
    {
        var count = await _database.SetLengthAsync(GetUserConnectionsKey(userId));
        return count > 0;
    }

    public async Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId)
    {
        var values = await _database.SetMembersAsync(GetUserConnectionsKey(userId));

        return values
            .Select(x => x.ToString())
            .ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> GetOnlineUsersAsync()
    {
        var values = await _database.SetMembersAsync(GetOnlineUsersKey());

        return values
            .Select(x => Guid.Parse((string)x!))
            .ToList();
    }

    private static string GetUserConnectionsKey(Guid userId)
        => $"presence:user:{userId}:connections";

    private static string GetConnectionUserKey(string connectionId)
        => $"presence:connection:{connectionId}:user";

    private static string GetOnlineUsersKey()
        => "presence:online-users";
}