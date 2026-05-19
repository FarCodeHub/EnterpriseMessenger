namespace Messenger.Contracts.Messages;

public sealed record MarkConversationReadRequest(
    Guid UserId,
    long LastReadSequenceNumber
);