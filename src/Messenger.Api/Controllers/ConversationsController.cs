using Messenger.Contracts.Conversations;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Conversations.CreatePrivateConversation;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class ConversationsController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly IPrivateConversationCreator _privateConversationCreator;

    public ConversationsController(MessengerDbContext dbContext, IPrivateConversationCreator privateConversationCreator)
    {
        _dbContext = dbContext;
        _privateConversationCreator = privateConversationCreator;
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
            .Where(member => member.UserId == userId)
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

        var conversationIds = conversations
            .Select(x => x.Id)
            .ToList();

        var unreadCounts = await _dbContext.Messages
            .Where(message =>
                conversationIds.Contains(message.ConversationId))
            .GroupJoin(
                _dbContext.ConversationMembers.Where(member => member.UserId == userId),
                message => message.ConversationId,
                member => member.ConversationId,
                (message, members) => new
                {
                    Message = message,
                    Member = members.FirstOrDefault()
                })
            .Where(x =>
                x.Member != null &&
                x.Message.SequenceNumber > x.Member.LastReadSequenceNumber &&
                x.Message.SenderUserId != userId)
            .GroupBy(x => x.Message.ConversationId)
            .Select(x => new
            {
                ConversationId = x.Key,
                Count = x.Count()
            })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

        var result = conversations.Select(x => new
        {
            x.Id,
            x.Type,
            x.Title,
            x.CreatedAtUtc,
            x.LastMessageId,
            x.LastMessageAtUtc,
            x.LastReadSequenceNumber,
            UnreadCount = unreadCounts.TryGetValue(x.Id, out var count) ? count : 0
        });

        return Ok(result);
    }



}