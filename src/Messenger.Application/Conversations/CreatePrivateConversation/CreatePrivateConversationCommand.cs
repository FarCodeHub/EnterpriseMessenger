namespace Messenger.Application.Conversations.CreatePrivateConversation;

public sealed record CreatePrivateConversationCommand(
    Guid FirstUserId,
    Guid SecondUserId
);