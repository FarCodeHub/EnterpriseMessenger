namespace Messenger.Contracts.Messages;

public sealed record MarkConversationReadRequest(
    long LastReadSequenceNumber
);