namespace Messenger.Application.Search;

public sealed record MessageSearchResultItem(
    MessageSearchDocument Document,
    string? Highlight);