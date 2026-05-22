namespace Messenger.Contracts.Conversations;

public sealed record SetConversationArchiveRequest(
    bool IsArchived
);