namespace Messenger.Contracts.Conversations;

public sealed record ConversationMemberSettingsResponse(
    Guid ConversationId,
    Guid UserId,
    bool IsMuted,
    bool IsPinned,
    bool IsArchived
);