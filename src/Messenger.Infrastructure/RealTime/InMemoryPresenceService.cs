using Messenger.Application.Abstractions.Realtime;

namespace Messenger.Infrastructure.Realtime;

/// <summary>
/// Presence kept in this process, used when Redis is not configured (development, a single API instance).
/// Same rules as <see cref="RedisPresenceService"/>: a connection is in a conversation while its lease is renewed
/// at least every 90 seconds. With several API instances use Redis, otherwise each instance sees only its own users.
/// </summary>
public sealed class InMemoryPresenceService : IPresenceService
{
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(90);

    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, HashSet<string>> _userConnections = [];

    // (conversation, user, connection) -> lease expiry
    private readonly Dictionary<(Guid ConversationId, Guid UserId, string ConnectionId), DateTimeOffset> _leases = [];

    public InMemoryPresenceService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task SetOnlineAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var connections))
                _userConnections[userId] = connections = [];

            connections.Add(connectionId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> RemoveConnectionAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out var connections))
                return Task.FromResult(true);

            connections.Remove(connectionId);

            if (connections.Count > 0)
                return Task.FromResult(false);

            _userConnections.Remove(userId);
            return Task.FromResult(true);
        }
    }

    public Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult(_userConnections.ContainsKey(userId));
    }

    public Task JoinConversationAsync(
        Guid conversationId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken) =>
        RenewConversationPresenceAsync(conversationId, userId, connectionId, cancellationToken);

    public Task RenewConversationPresenceAsync(
        Guid conversationId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
            _leases[(conversationId, userId, connectionId)] = _timeProvider.GetUtcNow() + PresenceTtl;

        return Task.CompletedTask;
    }

    /// <returns>True when the user has no other live connection in the conversation.</returns>
    public Task<bool> LeaveConversationAsync(
        Guid conversationId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _leases.Remove((conversationId, userId, connectionId));
            RemoveExpired();

            return Task.FromResult(!_leases.Keys.Any(x => x.ConversationId == conversationId && x.UserId == userId));
        }
    }

    public Task<IReadOnlyCollection<Guid>> GetOnlineConversationUserIdsAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            RemoveExpired();

            IReadOnlyCollection<Guid> userIds = _leases.Keys
                .Where(x => x.ConversationId == conversationId)
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            return Task.FromResult(userIds);
        }
    }

    public Task<IReadOnlyCollection<Guid>> GetConnectionConversationIdsAsync(
        string connectionId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            RemoveExpired();

            IReadOnlyCollection<Guid> conversationIds = _leases.Keys
                .Where(x => x.ConnectionId == connectionId)
                .Select(x => x.ConversationId)
                .Distinct()
                .ToList();

            return Task.FromResult(conversationIds);
        }
    }

    // Caller holds _lock.
    private void RemoveExpired()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var expired in _leases.Where(x => x.Value <= now).Select(x => x.Key).ToList())
            _leases.Remove(expired);
    }
}
