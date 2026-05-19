namespace Messenger.Application.Conversations.CreatePrivateConversation;

public sealed record CreatePrivateConversationResult(
    Guid Id,
    int Type,
    IReadOnlyCollection<Guid> Members
);