namespace Messenger.Application.Conversations.SetMemberRole;

public sealed record SetConversationMemberRoleCommand(
    Guid ConversationId,
    Guid CurrentUserId,
    Guid TargetUserId,
    int Role
);