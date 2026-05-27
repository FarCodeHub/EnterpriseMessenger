namespace Messenger.Contracts.Realtime;

public sealed record ConversationUpdatedEvent(
    Guid ConversationId,
    Guid LastMessageId,
    string Body,
    Guid SenderUserId,
    long SequenceNumber,
    DateTime CreatedAtUtc
);