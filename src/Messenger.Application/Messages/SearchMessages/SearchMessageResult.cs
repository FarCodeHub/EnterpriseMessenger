namespace Messenger.Application.Messages.SearchMessages;

public sealed record SearchMessageResult(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    string Body,
    Guid? ReplyToMessageId,
    long? ReplyToSequenceNumber,
    DateTime CreatedAtUtc
);