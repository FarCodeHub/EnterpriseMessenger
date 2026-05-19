using Messenger.Application.Sync.CommitSync;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Sync;

public sealed class SyncCommitter : ISyncCommitter
{
    private readonly MessengerDbContext _dbContext;

    public SyncCommitter(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CommitSyncResult> CommitAsync(
        CommitSyncCommand command,
        CancellationToken cancellationToken)
    {
        var device = await _dbContext.UserDevices
            .FirstOrDefaultAsync(
                x => x.UserId == command.UserId &&
                     x.DeviceId == command.DeviceId,
                cancellationToken);

        if (device is null)
            throw new InvalidOperationException("Device not found.");

        if (command.LastSyncedEventId < device.LastSyncedEventId)
            throw new InvalidOperationException("lastSyncedEventId cannot move backward.");

        device.UpdateLastSyncedEventId(command.LastSyncedEventId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CommitSyncResult(
            device.UserId,
            device.DeviceId,
            device.LastSyncedEventId);
    }
}