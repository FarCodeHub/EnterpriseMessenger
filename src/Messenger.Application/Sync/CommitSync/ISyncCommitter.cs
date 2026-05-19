namespace Messenger.Application.Sync.CommitSync;

public interface ISyncCommitter
{
    Task<CommitSyncResult> CommitAsync(
        CommitSyncCommand command,
        CancellationToken cancellationToken);
}