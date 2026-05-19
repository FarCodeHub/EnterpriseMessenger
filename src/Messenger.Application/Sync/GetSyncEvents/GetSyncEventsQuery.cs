namespace Messenger.Application.Sync.GetSyncEvents;

public sealed record GetSyncEventsQuery(
    Guid UserId,
    string DeviceId,
    long AfterEventId,
    int Take
);