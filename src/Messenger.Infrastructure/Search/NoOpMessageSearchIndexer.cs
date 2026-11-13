using Messenger.Application.Search;

namespace Messenger.Infrastructure.Search;

/// <summary>
/// Used when OpenSearch is not configured (<c>OpenSearch:Url</c> is empty). Writes are skipped, so sending,
/// editing and deleting messages keep working; search falls back to <see cref="PostgresMessageFullTextSearchService"/>.
/// </summary>
public sealed class NoOpMessageSearchIndexer : IMessageSearchIndexer
{
    public Task IndexAsync(MessageSearchDocument document, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task DeleteAsync(Guid messageId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task EnsureIndexAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task RebuildAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "OpenSearch is not configured (OpenSearch:Url is empty), so there is no search index to rebuild.");
}
