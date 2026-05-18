namespace Messenger.Contracts.Conversations;

public sealed record CreateGroupConversationRequest(
    string Title,
    Guid CreatedByUserId,
    List<Guid> MemberUserIds
);