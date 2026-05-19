namespace Messenger.Application.Messages.SearchMessages;

public sealed record SearchMessagesResult(
    Guid ConversationId,
    string Query,
    int Count,
    IReadOnlyCollection<SearchMessageResult> Results
);