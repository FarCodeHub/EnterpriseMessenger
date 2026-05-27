using Messenger.Application.Search;

namespace Messenger.IntegrationTests.Fakes;

public sealed class FakeMessageFullTextSearchService : IMessageFullTextSearchService
{
    public Task<MessageSearchResult> SearchAsync(
        MessageSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new MessageSearchResult(
            query.Page,
            query.PageSize,
            0,
            Array.Empty<MessageSearchDocument>()));
    }
}