namespace Messenger.Application.Sync.CommitSync;

public sealed record CommitSyncCommand(
    Guid UserId,
    string DeviceId,
    long LastSyncedEventId
);