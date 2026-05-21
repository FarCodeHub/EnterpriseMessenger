namespace Messenger.Contracts.Conversations;

public sealed record AddConversationMemberRequest(
    Guid UserId
);