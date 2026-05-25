namespace Messenger.Contracts.Realtime;

public sealed record TypingEvent(
    Guid ConversationId,
    Guid UserId
);