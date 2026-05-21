namespace Messenger.Application.Conversations.GetMembers;

public sealed record ConversationMemberResult(
    Guid Id,
    string DisplayName,
    string UserName,
    DateTime JoinedAtUtc,
    bool IsMuted,
    bool IsPinned,
    bool IsArchived,
    string Role
);