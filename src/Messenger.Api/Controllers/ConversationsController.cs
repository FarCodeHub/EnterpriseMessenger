using Messenger.Contracts.Conversations;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;

    public ConversationsController(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("private")]
    public async Task<IActionResult> CreatePrivateConversation(
        CreatePrivateConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FirstUserId == request.SecondUserId)
            return BadRequest("Private conversation requires two different users.");

        var userIds = new[] { request.FirstUserId, request.SecondUserId };

        var existingUsersCount = await _dbContext.Users
            .CountAsync(x => userIds.Contains(x.Id), cancellationToken);

        if (existingUsersCount != 2)
            return BadRequest("One or both users do not exist.");

        var conversation = new Conversation(
            ConversationType.Private,
            request.FirstUserId);

        _dbContext.Conversations.Add(conversation);


        _dbContext.ConversationCounters.Add(
            new ConversationCounter(conversation.Id));

        _dbContext.ConversationMembers.AddRange(
            new ConversationMember(conversation.Id, request.FirstUserId),
            new ConversationMember(conversation.Id, request.SecondUserId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            conversation.Id,
            conversation.Type,
            Members = userIds
        });
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroupConversation(
        CreateGroupConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Group title is required.");

        var memberUserIds = request.MemberUserIds
            .Append(request.CreatedByUserId)
            .Distinct()
            .ToList();

        if (memberUserIds.Count < 2)
            return BadRequest("Group requires at least two members.");

        var existingUsersCount = await _dbContext.Users
            .CountAsync(x => memberUserIds.Contains(x.Id), cancellationToken);

        if (existingUsersCount != memberUserIds.Count)
            return BadRequest("Some users do not exist.");

        var conversation = new Conversation(
            ConversationType.Group,
            request.CreatedByUserId,
            request.Title);

        _dbContext.Conversations.Add(conversation);

        _dbContext.ConversationCounters.Add(
            new ConversationCounter(conversation.Id));

        foreach (var userId in memberUserIds)
        {
            _dbContext.ConversationMembers.Add(
                new ConversationMember(conversation.Id, userId));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            conversation.Id,
            conversation.Type,
            conversation.Title,
            Members = memberUserIds
        });
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserConversations(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var conversations = await _dbContext.ConversationMembers
            .Where(x => x.UserId == userId)
            .Join(
                _dbContext.Conversations,
                member => member.ConversationId,
                conversation => conversation.Id,
                (member, conversation) => new
                {
                    conversation.Id,
                    conversation.Type,
                    conversation.Title,
                    conversation.CreatedAtUtc,
                    conversation.LastMessageId,
                    conversation.LastMessageAtUtc,
                    member.LastReadSequenceNumber
                })
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(conversations);
    }
}