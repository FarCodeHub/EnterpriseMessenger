namespace Messenger.Application.Search;

public interface IMessageFullTextSearchService
{
    Task<MessageSearchResult> SearchAsync(
        MessageSearchQuery query,
        CancellationToken cancellationToken);
}