namespace Messenger.Application.Conversations.CreateGroupConversation;

public sealed record CreateGroupConversationCommand(
    string Title,
    Guid CreatedByUserId,
    List<Guid> MemberUserIds
);