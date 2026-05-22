namespace Messenger.Contracts.Conversations;

public sealed record SetConversationMuteRequest(
    bool IsMuted
);