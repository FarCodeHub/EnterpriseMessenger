using Messenger.Contracts.Messages;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly IHubContext<ChatHub> _hubContext;

    public MessagesController(
        MessengerDbContext dbContext,
        IHubContext<ChatHub> hubContext)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("Message body is required.");

        var isMember = await _dbContext.ConversationMembers
            .AnyAsync(x =>
                x.ConversationId == request.ConversationId &&
                x.UserId == request.SenderUserId,
                cancellationToken);

        if (!isMember)
            return BadRequest("Sender is not a member of this conversation.");

        var duplicateMessage = await _dbContext.Messages
            .FirstOrDefaultAsync(x =>
                x.ConversationId == request.ConversationId &&
                x.ClientMessageId == request.ClientMessageId,
                cancellationToken);

        if (duplicateMessage is not null)
        {
            return Ok(new
            {
                duplicateMessage.Id,
                duplicateMessage.ConversationId,
                duplicateMessage.SequenceNumber,
                duplicateMessage.SenderUserId,
                duplicateMessage.ClientMessageId,
                duplicateMessage.Body,
                duplicateMessage.CreatedAtUtc,
                IsDuplicate = true
            });
        }

        long? replyToSequenceNumber = null;

        if (request.ReplyToMessageId is not null)
        {
            var replyToMessage = await _dbContext.Messages
                .Where(x =>
                    x.Id == request.ReplyToMessageId &&
                    x.ConversationId == request.ConversationId)
                .Select(x => new
                {
                    x.Id,
                    x.SequenceNumber
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (replyToMessage is null)
                return BadRequest("Reply target message not found.");

            replyToSequenceNumber = replyToMessage.SequenceNumber;
        }

        var counter = await _dbContext.ConversationCounters
            .FirstOrDefaultAsync(x => x.ConversationId == request.ConversationId, cancellationToken);

        if (counter is null)
            return BadRequest("Conversation counter not found.");

        var nextSequenceNumber = counter.GetNextSequenceNumber();

        var message = new Message(
            request.ConversationId,
            nextSequenceNumber,
            request.SenderUserId,
            request.ClientMessageId,
            request.Body,
            request.ReplyToMessageId,
            replyToSequenceNumber);

        _dbContext.Messages.Add(message);

var memberUserIds = await _dbContext.ConversationMembers
    .Where(x => x.ConversationId == request.ConversationId)
    .Select(x => x.UserId)
    .ToListAsync(cancellationToken);

foreach (var memberUserId in memberUserIds)
{
    _dbContext.MessageDeliveries.Add(
        new MessageDelivery(message.Id, memberUserId));
}


        await _dbContext.SaveChangesAsync(cancellationToken);


        var response = new
        {
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.SenderUserId,
            message.ClientMessageId,
            message.Body,
            message.ReplyToMessageId,
            message.ReplyToSequenceNumber,
            message.CreatedAtUtc,
            IsDuplicate = false
        };

        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync("message.received", response, cancellationToken);

        return Ok(response);


    }

    [HttpGet("conversation/{conversationId:guid}")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        long? beforeSequence,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var query = _dbContext.Messages
            .Where(x => x.ConversationId == conversationId);

        if (beforeSequence is not null)
        {
            query = query.Where(x => x.SequenceNumber < beforeSequence.Value);
        }

        var messages = await query
            .OrderByDescending(x => x.SequenceNumber)
            .Take(take)
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.SequenceNumber,
                x.SenderUserId,
                x.Body,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }

    [HttpGet("conversation/{conversationId:guid}/around/{sequenceNumber:long}")]
    public async Task<IActionResult> GetMessagesAround(
        Guid conversationId,
        long sequenceNumber,
        int radius = 100,
        CancellationToken cancellationToken = default)
    {
        radius = Math.Clamp(radius, 10, 200);

        var from = sequenceNumber - radius;
        var to = sequenceNumber + radius;

        var messages = await _dbContext.Messages
            .Where(x =>
                x.ConversationId == conversationId &&
                x.SequenceNumber >= from &&
                x.SequenceNumber <= to)
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.SequenceNumber,
                x.SenderUserId,
                x.Body,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }
}