namespace Messenger.Api.Realtime;

public interface IConnectionTracker
{
    Task AddConnectionAsync(Guid userId, string connectionId);

    Task<Guid?> RemoveConnectionAsync(string connectionId);

    Task<bool> IsOnlineAsync(Guid userId);

    Task<IReadOnlyCollection<string>> GetConnectionsAsync(Guid userId);

    Task<IReadOnlyCollection<Guid>> GetOnlineUsersAsync();
}