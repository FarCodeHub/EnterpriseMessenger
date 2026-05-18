using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;

    public SyncController(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{userId:guid}/{deviceId}")]
    public async Task<IActionResult> GetSyncEvents(
        Guid userId,
        string deviceId,
        long afterEventId = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        var device = await _dbContext.UserDevices
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.DeviceId == deviceId,
                cancellationToken);

        if (device is null)
            return NotFound("Device not found.");

        var events = await _dbContext.UserSyncEvents
            .Where(x =>
                x.UserId == userId &&
                x.EventId > afterEventId)
            .OrderBy(x => x.EventId)
            .Take(take)
            .Select(x => new
            {
                x.EventId,
                x.EventType,
                x.ConversationId,
                x.MessageId,
                x.PayloadJson,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestEventId = events.Count == 0
            ? afterEventId
            : events[^1].EventId;

        return Ok(new
        {
            UserId = userId,
            DeviceId = deviceId,
            AfterEventId = afterEventId,
            LatestEventId = latestEventId,
            HasMore = events.Count == take,
            Events = events
        });
    }

    [HttpPost("{userId:guid}/{deviceId}/commit")]
    public async Task<IActionResult> CommitSync(
        Guid userId,
        string deviceId,
        long lastSyncedEventId,
        CancellationToken cancellationToken)
    {
        var device = await _dbContext.UserDevices
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.DeviceId == deviceId,
                cancellationToken);

        if (device is null)
            return NotFound("Device not found.");

        if (lastSyncedEventId < device.LastSyncedEventId)
            return BadRequest("lastSyncedEventId cannot move backward.");

        device.UpdateLastSyncedEventId(lastSyncedEventId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            device.UserId,
            device.DeviceId,
            device.LastSyncedEventId
        });
    }
}