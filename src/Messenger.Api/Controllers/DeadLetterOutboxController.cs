using Messenger.Contracts.Common;
using Messenger.Contracts.Outbox;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Messenger.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class DeadLetterOutboxController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;

    public DeadLetterOutboxController(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetDeadLetters(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.DeadLetterOutboxMessages
            .OrderByDescending(x => x.MovedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.OriginalOutboxMessageId,
                x.Type,
                x.Error,
                x.RetryCount,
                x.MovedAtUtc
            })
            .ToListAsync(cancellationToken);

return Ok(new PagedResponse<DeadLetterOutboxResponse>(
    page,
    pageSize,
    totalCount,
    items.Select(x => new DeadLetterOutboxResponse(
        x.Id,
        x.OriginalOutboxMessageId,
        x.Type,
        x.Error,
        x.RetryCount,
        x.MovedAtUtc)).ToList()));
    }


[HttpPost("{deadLetterId:guid}/retry")]
public async Task<IActionResult> RetryDeadLetter(
    Guid deadLetterId,
    CancellationToken cancellationToken)
{
    var deadLetter = await _dbContext.DeadLetterOutboxMessages
        .FirstOrDefaultAsync(x => x.Id == deadLetterId, cancellationToken);

    if (deadLetter is null)
        return NotFound("Dead-letter outbox message not found.");

    var outboxMessage = new Messenger.Domain.Outbox.OutboxMessage(
        deadLetter.Type,
        deadLetter.Payload);

    _dbContext.OutboxMessages.Add(outboxMessage);
    _dbContext.DeadLetterOutboxMessages.Remove(deadLetter);

    await _dbContext.SaveChangesAsync(cancellationToken);

return Ok(new RetryDeadLetterOutboxResponse(
    deadLetterId,
    outboxMessage.Id,
    DateTime.UtcNow));
}


}