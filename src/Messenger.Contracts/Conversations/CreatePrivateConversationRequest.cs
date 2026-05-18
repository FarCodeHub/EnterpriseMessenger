namespace Messenger.Contracts.Conversations;

public sealed record CreatePrivateConversationRequest(
    Guid FirstUserId,
    Guid SecondUserId
);