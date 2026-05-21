namespace Messenger.Application.Conversations.LeaveConversation;

public sealed record LeaveConversationResult(
    Guid ConversationId,
    Guid UserId,
    DateTime LeftAtUtc
);