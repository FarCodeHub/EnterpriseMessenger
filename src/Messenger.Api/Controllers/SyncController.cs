using Messenger.Application.Sync.CommitSync;
using Messenger.Application.Sync.GetSyncEvents;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class SyncController : ControllerBase
{
    private readonly ISyncEventReader _syncEventReader;
    private readonly ISyncCommitter _syncCommitter;

    public SyncController(
        ISyncEventReader syncEventReader,
        ISyncCommitter syncCommitter)
    {
        _syncEventReader = syncEventReader;
        _syncCommitter = syncCommitter;
    }

    [HttpGet("{userId:guid}/{deviceId}")]
    public async Task<IActionResult> GetSyncEvents(
        Guid userId,
        string deviceId,
        long afterEventId = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _syncEventReader.GetAsync(
                new GetSyncEventsQuery(
                    userId,
                    deviceId,
                    afterEventId,
                    take),
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{userId:guid}/{deviceId}/commit")]
    public async Task<IActionResult> CommitSync(
        Guid userId,
        string deviceId,
        long lastSyncedEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _syncCommitter.CommitAsync(
                new CommitSyncCommand(
                    userId,
                    deviceId,
                    lastSyncedEventId),
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}