namespace Messenger.Application.Messages.GetMessages;

public interface IMessageReader
{
    Task<IReadOnlyCollection<MessageListItemResult>> GetConversationMessagesAsync(
        Guid conversationId,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MessageListItemResult>> GetMessagesAroundAsync(
        Guid conversationId,
        long sequenceNumber,
        int radius,
        CancellationToken cancellationToken);

    Task<object> GetReplyContextAsync(
        Guid messageId,
        int radius,
        CancellationToken cancellationToken);
}