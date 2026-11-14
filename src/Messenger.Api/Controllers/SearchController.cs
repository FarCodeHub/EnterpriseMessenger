using Messenger.Api.Common;
using Messenger.Application.Search;
using Messenger.Contracts.Common;
using Messenger.Contracts.Search;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public sealed class SearchController : ControllerBase
{
    private readonly IMessageFullTextSearchService _messageSearchService;
    private readonly ISearchIndexRebuilder _searchIndexRebuilder;
    private readonly MessengerDbContext _dbContext;

    public SearchController(IMessageFullTextSearchService messageSearchService, ISearchIndexRebuilder searchIndexRebuilder, MessengerDbContext dbContext)
    {
        _messageSearchService = messageSearchService;
        _searchIndexRebuilder = searchIndexRebuilder;
        _dbContext = dbContext;
    }

    [HttpGet("messages")]
    public async Task<IActionResult> SearchMessages(
        [FromQuery] string query,
        [FromQuery] Guid? conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {


if (string.IsNullOrWhiteSpace(query))
{
    return BadRequest(new ApiErrorResponse(
        "validation_error",
        "Search query is required.",
        StatusCodes.Status400BadRequest,
        DateTime.UtcNow));
}

        var currentUserId = GetCurrentUserId();

        var result = await _messageSearchService.SearchAsync(
            new MessageSearchQuery(
                currentUserId,
                query,
                conversationId,
                page,
                pageSize),
            cancellationToken);

        return Ok(new PagedResponse<MessageSearchResponse>(
            result.Page,
            result.PageSize,
            (int)result.TotalCount,
            
  result.Items.Select(x => new MessageSearchResponse(
    x.Document.Id,
    x.Document.ConversationId,
    x.Document.SenderUserId,
    x.Document.Body,
    x.Highlight,
    x.Document.SequenceNumber,
    x.Document.CreatedAtUtc,
    x.Document.EditedAtUtc)).ToList()));
    }


    /// <summary>
    /// Starts rebuilding the message search index in the background (it takes minutes on large data) and answers
    /// right away; follow it with <c>GET messages/rebuild-index</c>.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("messages/rebuild-index")]
    public IActionResult RebuildMessagesIndex()
    {
        return _searchIndexRebuilder.Start() switch
        {
            SearchIndexRebuildStart.Started => Accepted(_searchIndexRebuilder.Status),
            SearchIndexRebuildStart.AlreadyRunning => Conflict(_searchIndexRebuilder.Status),
            _ => BadRequest(new ApiErrorResponse(
                "search_not_configured",
                "OpenSearch is not configured (OpenSearch:Url is empty), so there is no search index to rebuild.",
                StatusCodes.Status400BadRequest,
                DateTime.UtcNow)),
        };
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("messages/rebuild-index")]
    public IActionResult GetRebuildStatus() => Ok(_searchIndexRebuilder.Status);

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Invalid user id.");

        return userId;
    }


[HttpGet("conversations")]
public async Task<IActionResult> SearchConversations(
    [FromQuery] string query,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return BadRequest(new ApiErrorResponse(
            "validation_error",
            "Search query is required.",
            StatusCodes.Status400BadRequest,
            DateTime.UtcNow));
    }

    var currentUserId = GetCurrentUserId();

    page = page <= 0 ? 1 : page;
    pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

var searchText = query.Trim().ToLower();

var baseQuery = _dbContext.Conversations
    .Where(c =>
        _dbContext.ConversationMembers.Any(m =>
            m.ConversationId == c.Id &&
            m.UserId == currentUserId &&
            m.LeftAtUtc == null) &&
        (
            c.Title != null &&
            c.Title.ToLower().Contains(searchText)
            ||
            _dbContext.ConversationMembers.Any(m =>
                m.ConversationId == c.Id &&
                m.UserId != currentUserId &&
                m.LeftAtUtc == null &&
                _dbContext.Users.Any(u =>
                    u.Id == m.UserId &&
                    (
                        u.DisplayName.ToLower().Contains(searchText) ||
                        u.UserName.ToLower().Contains(searchText)
                    )))
        ));

    var totalCount = await baseQuery.CountAsync(cancellationToken);

    var items = await baseQuery
        .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new ConversationSearchResponse(
            x.Id,
            x.Title,
            x.Type.ToString(),
            x.CreatedAtUtc,
            x.LastMessageAtUtc))
        .ToListAsync(cancellationToken);

    return Ok(new PagedResponse<ConversationSearchResponse>(
        page,
        pageSize,
        totalCount,
        items));
}
}