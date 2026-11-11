using Messenger.Application.Sync.CommitSync;
using Messenger.Application.Sync.GetSyncEvents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Messenger.Api.Controllers;
[Authorize]
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
        // Sync events contain message bodies: only the owner of the account may read or commit them.
        if (!IsCurrentUser(userId))
            return Forbid();

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
        // Sync events contain message bodies: only the owner of the account may read or commit them.
        if (!IsCurrentUser(userId))
            return Forbid();

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

    private bool IsCurrentUser(Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var currentUserId) && currentUserId == userId;
    }
}
