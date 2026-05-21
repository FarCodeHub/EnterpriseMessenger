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


    [HttpGet("archived")]
    public async Task<IActionResult> GetArchivedConversations(
    CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _userConversationReader.GetArchivedAsync(
            new GetUserConversationsQuery(currentUserId),
            cancellationToken);

        return Ok(result);
    }



    [HttpPost("{conversationId:guid}/leave")]
    public async Task<IActionResult> LeaveConversation(
    Guid conversationId,
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

      member.Leave();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ConversationId = conversationId,
            UserId = currentUserId,
            LeftAtUtc = DateTime.UtcNow
        });
    }


    [HttpPost("{conversationId:guid}/members")]
public async Task<IActionResult> AddMember(
    Guid conversationId,
    AddConversationMemberRequest request,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var conversation = await _dbContext.Conversations
        .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

    if (conversation is null)
        return NotFound("Conversation not found.");

   var currentMember = await _dbContext.ConversationMembers
    .FirstOrDefaultAsync(x =>
        x.ConversationId == conversationId &&
        x.UserId == currentUserId &&
        x.LeftAtUtc == null,
        cancellationToken);

if (currentMember is null ||
    (currentMember.Role != ConversationMemberRole.Owner &&
     currentMember.Role != ConversationMemberRole.Admin))
{
    return StatusCode(StatusCodes.Status403Forbidden, "Only owner or admin can manage members.");
}

    var userExists = await _dbContext.Users
        .AnyAsync(x => x.Id == request.UserId, cancellationToken);

    if (!userExists)
        return BadRequest("User not found.");

    var existingMember = await _dbContext.ConversationMembers
        .FirstOrDefaultAsync(x =>
            x.ConversationId == conversationId &&
            x.UserId == request.UserId,
            cancellationToken);

    if (existingMember is not null)
    {
        existingMember.Rejoin();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ConversationId = conversationId,
            request.UserId,
            Rejoined = true
        });
    }

    _dbContext.ConversationMembers.Add(
        new ConversationMember(conversationId, request.UserId));

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        ConversationId = conversationId,
        request.UserId,
        Rejoined = false
    });
}


[HttpDelete("{conversationId:guid}/members/{userId:guid}")]
public async Task<IActionResult> RemoveMember(
    Guid conversationId,
    Guid userId,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var conversation = await _dbContext.Conversations
        .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

    if (conversation is null)
        return NotFound("Conversation not found.");

   var currentMember = await _dbContext.ConversationMembers
    .FirstOrDefaultAsync(x =>
        x.ConversationId == conversationId &&
        x.UserId == currentUserId &&
        x.LeftAtUtc == null,
        cancellationToken);

if (currentMember is null ||
    (currentMember.Role != ConversationMemberRole.Owner &&
     currentMember.Role != ConversationMemberRole.Admin))
{
    return StatusCode(StatusCodes.Status403Forbidden, "Only owner or admin can manage members.");
}

    if (userId == conversation.CreatedByUserId)
        return BadRequest("Conversation creator cannot be removed.");

    var member = await _dbContext.ConversationMembers
        .FirstOrDefaultAsync(x =>
            x.ConversationId == conversationId &&
            x.UserId == userId,
            cancellationToken);

    if (member is null)
        return NotFound("Member not found.");

    member.Leave();

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        ConversationId = conversationId,
        UserId = userId,
        RemovedAtUtc = DateTime.UtcNow
    });
}


[HttpGet("{conversationId:guid}/members")]
public async Task<IActionResult> GetMembers(
    Guid conversationId,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var isCurrentUserMember = await _dbContext.ConversationMembers
        .AnyAsync(x =>
            x.ConversationId == conversationId &&
            x.UserId == currentUserId &&
            x.LeftAtUtc == null,
            cancellationToken);

    if (!isCurrentUserMember)
        return StatusCode(StatusCodes.Status403Forbidden, "You are not a member of this conversation.");

    var members = await _dbContext.ConversationMembers
        .Where(x =>
            x.ConversationId == conversationId &&
            x.LeftAtUtc == null)
        .Join(
            _dbContext.Users,
            member => member.UserId,
            user => user.Id,
            (member, user) => new
            {
                user.Id,
                user.DisplayName,
                user.UserName,
                member.JoinedAtUtc,
                member.IsMuted,
                member.IsPinned,
                member.IsArchived
            })
        .OrderBy(x => x.DisplayName)
        .ToListAsync(cancellationToken);

    return Ok(members);
}

}