namespace Messenger.Contracts.Conversations;

public sealed record SetConversationPinRequest(
    bool IsPinned
);