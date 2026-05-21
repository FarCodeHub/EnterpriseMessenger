namespace Messenger.Application.Conversations.AddMember;

public sealed record AddConversationMemberResult(
    Guid ConversationId,
    Guid UserId,
    bool Rejoined
);