namespace Messenger.Application.Sync.GetSyncEvents;

public interface ISyncEventReader
{
    Task<GetSyncEventsResult> GetAsync(
        GetSyncEventsQuery query,
        CancellationToken cancellationToken);
}