namespace Messenger.Application.Outbox;

public interface IOutboxPublisher
{
    /// <summary>
    /// Publishes up to <paramref name="batchSize"/> pending outbox events, oldest first, and marks them processed
    /// (or failed, for a later retry). Several API instances can run it at the same time: each event is claimed by
    /// one of them. Returns how many events were taken.
    /// </summary>
    Task<int> PublishBatchAsync(int batchSize, CancellationToken cancellationToken);
}
