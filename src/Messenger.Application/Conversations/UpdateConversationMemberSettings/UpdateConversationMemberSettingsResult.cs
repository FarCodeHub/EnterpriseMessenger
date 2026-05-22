namespace Messenger.Application.Conversations.UpdateConversationMemberSettings;

public sealed record UpdateConversationMemberSettingsResult(
    Guid ConversationId,
    Guid UserId,
    bool IsMuted,
    bool IsPinned,
    bool IsArchived
);