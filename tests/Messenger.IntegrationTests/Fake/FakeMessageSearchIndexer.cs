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


public Task RebuildAsync(CancellationToken cancellationToken)
{
    return Task.CompletedTask;
}
}