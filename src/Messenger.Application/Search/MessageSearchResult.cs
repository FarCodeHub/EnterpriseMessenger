using Messenger.Application.Search;

public sealed record MessageSearchResult(
    int Page,
    int PageSize,
    long TotalCount,
    IReadOnlyCollection<MessageSearchResultItem> Items);