using Messenger.Application.Sync.GetSyncEvents;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Sync;

public sealed class SyncEventReader : ISyncEventReader
{
    private readonly MessengerDbContext _dbContext;

    public SyncEventReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetSyncEventsResult> GetAsync(
        GetSyncEventsQuery query,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(query.Take, 1, 500);

        var deviceExists = await _dbContext.UserDevices
            .AnyAsync(x =>
                x.UserId == query.UserId &&
                x.DeviceId == query.DeviceId,
                cancellationToken);

        if (!deviceExists)
            throw new InvalidOperationException("Device not found.");

        var events = await _dbContext.UserSyncEvents
            .Where(x =>
                x.UserId == query.UserId &&
                x.EventId > query.AfterEventId)
            .OrderBy(x => x.EventId)
            .Take(take)
            .Select(x => new SyncEventResult(
                x.EventId,
                x.EventType,
                x.ConversationId,
                x.MessageId,
                x.PayloadJson,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var latestEventId = events.Count == 0
            ? query.AfterEventId
            : events[^1].EventId;

        return new GetSyncEventsResult(
            query.UserId,
            query.DeviceId,
            query.AfterEventId,
            latestEventId,
            events.Count == take,
            events);
    }
}