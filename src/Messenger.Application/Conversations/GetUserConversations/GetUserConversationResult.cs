namespace Messenger.Application.Conversations.GetUserConversations;

public sealed record GetUserConversationResult(
    Guid Id,
    int Type,
    string? Title,
    DateTime CreatedAtUtc,
    Guid? LastMessageId,
    DateTime? LastMessageAtUtc,
    long LastReadSequenceNumber,
    int UnreadCount,
    bool IsPinned
);