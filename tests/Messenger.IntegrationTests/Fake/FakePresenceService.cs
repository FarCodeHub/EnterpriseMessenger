using Messenger.Application.Abstractions.Realtime;

namespace Messenger.IntegrationTests.Fakes;

public sealed class FakePresenceService : IPresenceService
{
    private readonly HashSet<string> _connections = new();

    public Task SetOnlineAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        _connections.Add($"{userId}:{connectionId}");
        return Task.CompletedTask;
    }

    public Task<bool> RemoveConnectionAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        _connections.Remove($"{userId}:{connectionId}");
        return Task.FromResult(!_connections.Any(x => x.StartsWith($"{userId}:")));
    }

    public Task<bool> IsOnlineAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_connections.Any(x => x.StartsWith($"{userId}:")));
    }

    public Task JoinConversationAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken)
{
    return Task.CompletedTask;
}

public Task<bool> LeaveConversationAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken)
{
    return Task.FromResult(true);
}

public Task<IReadOnlyCollection<Guid>> GetOnlineConversationUserIdsAsync(
    Guid conversationId,
    CancellationToken cancellationToken)
{
    return Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
}


public Task<IReadOnlyCollection<Guid>> GetConnectionConversationIdsAsync(
    string connectionId,
    CancellationToken cancellationToken)
{
    return Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
}

public Task RenewConversationPresenceAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken)
{
    return Task.CompletedTask;
}


}