namespace Messenger.Application.Sync;

public interface IUserSyncEventWriter
{
    Task AddForConversationMembersAsync(
        Guid conversationId,
        Guid? messageId,
        string eventType,
        object payload,
        CancellationToken cancellationToken);

    /// <summary>
    /// Locks the sync counters of every active member of these conversations, in a fixed order.
    /// Call it inside the transaction before writing events for several conversations to avoid deadlocks.
    /// </summary>
    Task LockMemberCountersAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken);
}
