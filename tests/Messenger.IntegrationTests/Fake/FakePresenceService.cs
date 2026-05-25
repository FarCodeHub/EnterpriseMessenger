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
}