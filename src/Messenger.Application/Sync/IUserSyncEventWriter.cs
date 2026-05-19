namespace Messenger.Application.Sync;

public interface IUserSyncEventWriter
{
    Task AddForConversationMembersAsync(
        Guid conversationId,
        Guid? messageId,
        string eventType,
        object payload,
        CancellationToken cancellationToken);
}