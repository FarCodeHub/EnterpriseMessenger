namespace Messenger.Contracts.Realtime;

public sealed record MessageReceiptEvent(
    Guid ConversationId,
    Guid UserId,
    long SequenceNumber
);