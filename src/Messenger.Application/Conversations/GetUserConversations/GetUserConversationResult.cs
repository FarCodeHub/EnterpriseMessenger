namespace Messenger.Application.Conversations.GetUserConversations;

public sealed record GetUserConversationResult(
    Guid Id,
    int Type,
    string? Title,
    string DisplayTitle,
    Guid? OtherParticipantUserId,
    DateTime CreatedAtUtc,
    Guid? LastMessageId,
    DateTime? LastMessageAtUtc,
    string? LastMessagePreview,
    Guid? LastMessageSenderUserId,
    string? LastMessageSenderDisplayName,
    long LastReadSequenceNumber,
    long LastDeliveredSequenceNumber,
    long? OtherParticipantLastReadSequenceNumber,
    long? OtherParticipantLastDeliveredSequenceNumber,
    int UnreadCount,
    bool IsPinned,
    bool IsMuted,
    bool IsArchived);