namespace Messenger.Contracts.Conversations;

public sealed record ConversationMemberResponse(
    Guid Id,
    string DisplayName,
    string UserName,
    DateTime JoinedAtUtc,
    bool IsMuted,
    bool IsPinned,
    bool IsArchived,
    string Role
);