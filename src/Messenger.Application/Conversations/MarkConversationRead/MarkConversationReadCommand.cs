namespace Messenger.Application.Conversations.MarkConversationRead;

public sealed record MarkConversationReadCommand(
    Guid ConversationId,
    Guid UserId,
    long LastReadSequenceNumber
);