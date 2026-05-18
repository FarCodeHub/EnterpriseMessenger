using System.Collections.Concurrent;

namespace Messenger.Api.Realtime;

public sealed class InMemoryConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionUsers = new();

    public void AddConnection(Guid userId, string connectionId)
    {
        var connections = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());

        lock (connections)
        {
            connections.Add(connectionId);
        }

        _connectionUsers[connectionId] = userId;
    }

    public Guid? RemoveConnection(string connectionId)
    {
        if (!_connectionUsers.TryRemove(connectionId, out var userId))
            return null;

        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);

                if (connections.Count == 0)
                    _userConnections.TryRemove(userId, out _);
            }
        }

        return userId;
    }

    public bool IsOnline(Guid userId)
    {
        return _userConnections.ContainsKey(userId);
    }

    public IReadOnlyCollection<string> GetConnections(Guid userId)
    {
        if (!_userConnections.TryGetValue(userId, out var connections))
            return Array.Empty<string>();

        lock (connections)
        {
            return connections.ToArray();
        }
    }

    public IReadOnlyCollection<Guid> GetOnlineUsers()
    {
        return _userConnections.Keys.ToArray();
    }
}