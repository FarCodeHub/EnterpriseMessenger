using System.Collections.Concurrent;
using Messenger.Application.Realtime;

namespace Messenger.IntegrationTests.Fakes;

public sealed class FakeConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();

    public Task AddConnectionAsync(Guid userId, string connectionId)
    {
        _connections[connectionId] = userId;
        return Task.CompletedTask;
    }

    public Task<Guid?> RemoveConnectionAsync(string connectionId)
    {
        return Task.FromResult(_connections.TryRemove(connectionId, out var userId) ? userId : (Guid?)null);
    }

    public Task<bool> IsOnlineAsync(Guid userId)
    {
        return Task.FromResult(_connections.Values.Contains(userId));
    }

    public Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId)
    {
        IReadOnlyCollection<string> connections = _connections
            .Where(x => x.Value == userId)
            .Select(x => x.Key)
            .ToList();

        return Task.FromResult(connections);
    }

    public Task<IReadOnlyCollection<Guid>> GetOnlineUsersAsync()
    {
        IReadOnlyCollection<Guid> users = _connections.Values.Distinct().ToList();
        return Task.FromResult(users);
    }
}
