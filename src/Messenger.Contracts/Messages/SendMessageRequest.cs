namespace Messenger.Contracts.Messages;

public sealed record SendMessageRequest(
    Guid ConversationId,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId = null
);