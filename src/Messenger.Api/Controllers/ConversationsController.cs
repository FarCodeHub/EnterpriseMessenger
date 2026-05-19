using Messenger.Contracts.Conversations;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Conversations.CreatePrivateConversation;
using Messenger.Application.Conversations.CreateGroupConversation;
using Messenger.Application.Conversations.GetUserConversations;

namespace Messenger.Api.Controllers;

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

}