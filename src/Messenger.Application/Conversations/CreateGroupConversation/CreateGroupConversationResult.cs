namespace Messenger.Application.Conversations.CreateGroupConversation;

public sealed record CreateGroupConversationResult(
    Guid Id,
    int Type,
    string? Title,
    IReadOnlyCollection<Guid> Members
);