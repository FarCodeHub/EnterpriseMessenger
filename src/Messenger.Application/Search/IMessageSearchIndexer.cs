namespace Messenger.Application.Search;

public interface IMessageSearchIndexer
{
    Task IndexAsync(
        MessageSearchDocument document,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid messageId,
        CancellationToken cancellationToken);



    Task EnsureIndexAsync(CancellationToken cancellationToken);


    /// <summary>
    /// Indexes every message that is not deleted, in batches; <paramref name="onProgress"/> gets the running total.
    /// Returns how many messages were indexed. Slow on large data: run it through <see cref="ISearchIndexRebuilder"/>.
    /// </summary>
    Task<long> RebuildAsync(Action<long>? onProgress, CancellationToken cancellationToken);

}