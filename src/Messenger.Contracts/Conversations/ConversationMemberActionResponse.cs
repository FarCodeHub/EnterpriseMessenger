namespace Messenger.Contracts.Conversations;

public sealed record ConversationMemberActionResponse(
    Guid ConversationId,
    Guid UserId,
    bool? Rejoined = null,
    DateTime? LeftAtUtc = null,
    string? Role = null
);