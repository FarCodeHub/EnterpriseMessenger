namespace Messenger.Application.Abstractions.Realtime;

public interface IPresenceService
{
    Task SetOnlineAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken);

    Task<bool> RemoveConnectionAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken);

    Task<bool> IsOnlineAsync(
        Guid userId,
        CancellationToken cancellationToken);
}