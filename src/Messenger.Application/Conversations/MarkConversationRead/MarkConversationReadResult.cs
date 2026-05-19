namespace Messenger.Application.Conversations.MarkConversationRead;

public sealed record MarkConversationReadResult(
    Guid ConversationId,
    Guid UserId,
    long LastReadSequenceNumber,
    int SeenMessagesCount,
    DateTime ReadAtUtc
);