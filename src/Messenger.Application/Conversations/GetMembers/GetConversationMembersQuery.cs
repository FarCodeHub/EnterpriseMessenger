namespace Messenger.Application.Conversations.GetMembers;

public sealed record GetConversationMembersQuery(
    Guid ConversationId,
    Guid CurrentUserId
);