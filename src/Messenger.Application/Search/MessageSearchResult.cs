namespace Messenger.Application.Search;

public sealed record MessageSearchResult(
    int Page,
    int PageSize,
    long TotalCount,
    IReadOnlyCollection<MessageSearchDocument> Items);