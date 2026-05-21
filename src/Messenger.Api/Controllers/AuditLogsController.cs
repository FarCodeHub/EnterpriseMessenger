using Messenger.Contracts.Auditing;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;

    public AuditLogsController(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] AuditLogQueryRequest request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _dbContext.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EventType))
            query = query.Where(x => x.EventType == request.EventType);

        if (request.UserId is not null)
            query = query.Where(x => x.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(x => x.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.EntityId))
            query = query.Where(x => x.EntityId == request.EntityId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.UserId,
                x.EntityType,
                x.EntityId,
                x.Data,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }
}