namespace Messenger.Application.Conversations.RemoveMember;

public sealed record RemoveConversationMemberCommand(
    Guid ConversationId,
    Guid CurrentUserId,
    Guid UserIdToRemove
);