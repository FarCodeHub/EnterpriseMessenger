namespace Messenger.Application.Messages.SearchMessages;

public sealed record SearchMessagesQuery(
    Guid ConversationId,
    string Query,
    int Take
);