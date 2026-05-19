namespace Messenger.Application.Sync.CommitSync;

public sealed record CommitSyncResult(
    Guid UserId,
    string DeviceId,
    long LastSyncedEventId
);