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


    Task RebuildAsync(CancellationToken cancellationToken);

}