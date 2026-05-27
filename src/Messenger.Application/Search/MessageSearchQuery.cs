namespace Messenger.Application.Search;

public sealed record MessageSearchQuery(
    Guid CurrentUserId,
    string Text,
    Guid? ConversationId,
    int Page,
    int PageSize);