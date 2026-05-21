using Messenger.Contracts.Conversations;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Conversations.CreatePrivateConversation;
using Messenger.Application.Conversations.CreateGroupConversation;
using Messenger.Application.Conversations.GetUserConversations;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace Messenger.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class ConversationsController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly IGroupConversationCreator _groupConversationCreator;
    private readonly IPrivateConversationCreator _privateConversationCreator;
    private readonly IUserConversationReader _userConversationReader;

    public ConversationsController(MessengerDbContext dbContext, IPrivateConversationCreator privateConversationCreator, IGroupConversationCreator groupConversationCreator, IUserConversationReader userConversationReader)
    {
        _dbContext = dbContext;
        _privateConversationCreator = privateConversationCreator;
        _groupConversationCreator = groupConversationCreator;
        _privateConversationCreator = privateConversationCreator;
        _userConversationReader = userConversationReader;
    }


    [HttpPost("private")]
    public async Task<IActionResult> CreatePrivateConversation(
        CreatePrivateConversationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _privateConversationCreator.CreateAsync(
                new CreatePrivateConversationCommand(
                    request.FirstUserId,
                    request.SecondUserId),
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }



    [HttpPost("group")]
    public async Task<IActionResult> CreateGroupConversation(
       CreateGroupConversationRequest request,
       CancellationToken cancellationToken)
    {
        try
        {
            var result = await _groupConversationCreator.CreateAsync(
                new CreateGroupConversationCommand(
                    request.Title,
                    request.CreatedByUserId,
                    request.MemberUserIds),
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserConversations(
       Guid userId,
       CancellationToken cancellationToken)
    {
        var result = await _userConversationReader.GetAsync(
            new GetUserConversationsQuery(userId),
            cancellationToken);

        return Ok(result);
    }
    [HttpPost("{conversationId:guid}/mute")]
    public async Task<IActionResult> SetMute(
        Guid conversationId,
        SetConversationMuteRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == currentUserId,
                cancellationToken);

        if (member is null)
            return BadRequest("User is not a member of this conversation.");

        if (request.IsMuted)
            member.Mute();
        else
            member.Unmute();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ConversationId = conversationId,
            UserId = currentUserId,
            request.IsMuted
        });
    }
    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("UserId not found in token.");

        return Guid.Parse(userIdValue);
    }
    [HttpPost("{conversationId:guid}/pin")]
    public async Task<IActionResult> SetPin(
        Guid conversationId,
        SetConversationPinRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == currentUserId,
                cancellationToken);

        if (member is null)
            return BadRequest("User is not a member of this conversation.");

        if (request.IsPinned)
            member.Pin();
        else
            member.Unpin();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ConversationId = conversationId,
            UserId = currentUserId,
            request.IsPinned
        });
    }




    [HttpPost("{conversationId:guid}/archive")]
    public async Task<IActionResult> SetArchive(
    Guid conversationId,
    SetConversationArchiveRequest request,
    CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == currentUserId,
                cancellationToken);

        if (member is null)
            return BadRequest("User is not a member of this conversation.");

        if (request.IsArchived)
            member.Archive();
        else
            member.Unarchive();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ConversationId = conversationId,
            UserId = currentUserId,
            request.IsArchived
        });
    }
}