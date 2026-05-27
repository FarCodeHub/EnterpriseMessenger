using Messenger.Api.Common;
using Messenger.Application.Search;
using Messenger.Contracts.Common;
using Messenger.Contracts.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public sealed class SearchController : ControllerBase
{
    private readonly IMessageFullTextSearchService _messageSearchService;
    private readonly IMessageSearchIndexer _messageSearchIndexer;

    public SearchController(IMessageFullTextSearchService messageSearchService, IMessageSearchIndexer messageSearchIndexer)
    {
        _messageSearchService = messageSearchService;
        _messageSearchIndexer = messageSearchIndexer;
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


    [HttpPost("messages/rebuild-index")]
public async Task<IActionResult> RebuildMessagesIndex(
    CancellationToken cancellationToken)
{
    await _messageSearchIndexer.RebuildAsync(cancellationToken);

    return Ok(new
    {
        Message = "Messages search index rebuilt."
    });
}

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Invalid user id.");

        return userId;
    }





}