using Messenger.Contracts.Conversations;
using Messenger.Application.Conversations.AddMember;
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
using Messenger.Application.Conversations.RemoveMember;
using Messenger.Application.Conversations.SetMemberRole;
using Messenger.Application.Conversations.LeaveConversation;
using Messenger.Application.Conversations.UpdateConversationMemberSettings;
using Messenger.Application.Conversations.GetMembers;
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
    private readonly IConversationMemberAdder _conversationMemberAdder;
    private readonly IConversationMemberRemover _conversationMemberRemover;
    private readonly IConversationMemberRoleSetter _conversationMemberRoleSetter;
    private readonly IConversationLeaver _conversationLeaver;
    private readonly IConversationMemberSettingsUpdater _memberSettingsUpdater;
    private readonly IConversationMemberReader _conversationMemberReader;
    public ConversationsController(MessengerDbContext dbContext, IPrivateConversationCreator privateConversationCreator, IGroupConversationCreator groupConversationCreator, IUserConversationReader userConversationReader, IConversationMemberAdder conversationMemberAdder, IConversationMemberRemover conversationMemberRemover, IConversationMemberRoleSetter conversationMemberRoleSetter, IConversationLeaver conversationLeaver, IConversationMemberSettingsUpdater memberSettingsUpdater, IConversationMemberReader conversationMemberReader)
    {
        _dbContext = dbContext;
        _privateConversationCreator = privateConversationCreator;
        _groupConversationCreator = groupConversationCreator;
        _privateConversationCreator = privateConversationCreator;
        _userConversationReader = userConversationReader;
        _conversationMemberAdder = conversationMemberAdder;
        _conversationMemberRemover = conversationMemberRemover;
        _conversationMemberRoleSetter = conversationMemberRoleSetter;
        _conversationLeaver = conversationLeaver;
        _memberSettingsUpdater = memberSettingsUpdater;
        _conversationMemberReader = conversationMemberReader;
    }



    [HttpPost("{conversationId:guid}/members")]
public async Task<IActionResult> AddMember(
    Guid conversationId,
    AddConversationMemberRequest request,
    CancellationToken cancellationToken)
{
   
        var result = await _conversationMemberAdder.AddAsync(
            new AddConversationMemberCommand(
                conversationId,
                GetCurrentUserId(),
                request.UserId),
            cancellationToken);

        return Ok(result);
   
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
   
        var result = await _memberSettingsUpdater.UpdateAsync(
            new UpdateConversationMemberSettingsCommand(
                conversationId,
                GetCurrentUserId(),
                request.IsMuted,
                null,
                null),
            cancellationToken);

        return Ok(result);
 
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
    
        var result = await _memberSettingsUpdater.UpdateAsync(
            new UpdateConversationMemberSettingsCommand(
                conversationId,
                GetCurrentUserId(),
                null,
                request.IsPinned,
                null),
            cancellationToken);

        return Ok(result);
 
}

 [HttpPost("{conversationId:guid}/archive")]
public async Task<IActionResult> SetArchive(
    Guid conversationId,
    SetConversationArchiveRequest request,
    CancellationToken cancellationToken)
{
   
        var result = await _memberSettingsUpdater.UpdateAsync(
            new UpdateConversationMemberSettingsCommand(
                conversationId,
                GetCurrentUserId(),
                null,
                null,
                request.IsArchived),
            cancellationToken);

        return Ok(result);
   
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
   
        var result = await _conversationLeaver.LeaveAsync(
            new LeaveConversationCommand(
                conversationId,
                GetCurrentUserId()),
            cancellationToken);

        return Ok(result);
   
}
 
 
 
 [HttpDelete("{conversationId:guid}/members/{userId:guid}")]
public async Task<IActionResult> RemoveMember(
    Guid conversationId,
    Guid userId,
    CancellationToken cancellationToken)
{
   
        var result = await _conversationMemberRemover.RemoveAsync(
            new RemoveConversationMemberCommand(
                conversationId,
                GetCurrentUserId(),
                userId),
            cancellationToken);

        return Ok(result);
 
}
 [HttpGet("{conversationId:guid}/members")]
public async Task<IActionResult> GetMembers(
    Guid conversationId,
    CancellationToken cancellationToken)
{
    
        var result = await _conversationMemberReader.GetAsync(
            new GetConversationMembersQuery(
                conversationId,
                GetCurrentUserId()),
            cancellationToken);

        return Ok(result);
  
}
 [HttpPut("{conversationId:guid}/members/{userId:guid}/role")]
public async Task<IActionResult> SetMemberRole(
    Guid conversationId,
    Guid userId,
    SetConversationMemberRoleRequest request,
    CancellationToken cancellationToken)
{
    
        var result = await _conversationMemberRoleSetter.SetAsync(
            new SetConversationMemberRoleCommand(
                conversationId,
                GetCurrentUserId(),
                userId,
                request.Role),
            cancellationToken);

        return Ok(result);
   
}


}