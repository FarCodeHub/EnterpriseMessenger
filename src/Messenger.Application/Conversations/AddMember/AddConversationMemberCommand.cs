namespace Messenger.Application.Conversations.AddMember;

public sealed record AddConversationMemberCommand(
    Guid ConversationId,
    Guid CurrentUserId,
    Guid UserIdToAdd
);