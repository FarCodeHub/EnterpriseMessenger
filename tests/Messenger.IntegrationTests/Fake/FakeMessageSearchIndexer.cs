using Messenger.Application.Search;

namespace Messenger.IntegrationTests.Fakes;

public sealed class FakeMessageSearchIndexer : IMessageSearchIndexer
{
    public Task IndexAsync(
        MessageSearchDocument document,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }


    public Task EnsureIndexAsync(CancellationToken cancellationToken)
{
    return Task.CompletedTask;
}


public Task<long> RebuildAsync(Action<long>? onProgress, CancellationToken cancellationToken)
{
    onProgress?.Invoke(0);
    return Task.FromResult(0L);
}
}