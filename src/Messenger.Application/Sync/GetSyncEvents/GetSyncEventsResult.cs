namespace Messenger.Application.Sync.GetSyncEvents;

public sealed record GetSyncEventsResult(
    Guid UserId,
    string DeviceId,
    long AfterEventId,
    long LatestEventId,
    bool HasMore,
    IReadOnlyCollection<SyncEventResult> Events
);