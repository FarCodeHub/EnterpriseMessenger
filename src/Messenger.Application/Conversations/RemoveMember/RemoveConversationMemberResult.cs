namespace Messenger.Application.Conversations.RemoveMember;

public sealed record RemoveConversationMemberResult(
    Guid ConversationId,
    Guid UserId,
    DateTime RemovedAtUtc
);