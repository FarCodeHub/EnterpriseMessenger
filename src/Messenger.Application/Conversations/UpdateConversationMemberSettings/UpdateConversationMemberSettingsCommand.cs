namespace Messenger.Application.Conversations.UpdateConversationMemberSettings;

public sealed record UpdateConversationMemberSettingsCommand(
    Guid ConversationId,
    Guid UserId,
    bool? IsMuted,
    bool? IsPinned,
    bool? IsArchived
);