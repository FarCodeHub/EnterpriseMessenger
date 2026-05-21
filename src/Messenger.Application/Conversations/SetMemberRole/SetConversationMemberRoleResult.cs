namespace Messenger.Application.Conversations.SetMemberRole;

public sealed record SetConversationMemberRoleResult(
    Guid ConversationId,
    Guid UserId,
    string Role
);