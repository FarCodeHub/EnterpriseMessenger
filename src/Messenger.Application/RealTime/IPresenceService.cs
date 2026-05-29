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

        Task JoinConversationAsync(
        Guid conversationId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken);

    Task<bool> LeaveConversationAsync(
        Guid conversationId,
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetOnlineConversationUserIdsAsync(
        Guid conversationId,
        CancellationToken cancellationToken);

        Task<IReadOnlyCollection<Guid>> GetConnectionConversationIdsAsync(
    string connectionId,
    CancellationToken cancellationToken);


    Task RenewConversationPresenceAsync(
    Guid conversationId,
    Guid userId,
    string connectionId,
    CancellationToken cancellationToken);
}