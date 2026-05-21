namespace Messenger.Application.Conversations.LeaveConversation;

public sealed record LeaveConversationCommand(
    Guid ConversationId,
    Guid UserId
);