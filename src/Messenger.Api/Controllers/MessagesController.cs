using Messenger.Contracts.Messages;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;

    public MessagesController(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var lastSequenceNumber = await _dbContext.Messages
            .Where(x => x.ConversationId == request.ConversationId)
            .MaxAsync(x => (long?)x.SequenceNumber, cancellationToken) ?? 0;

        var nextSequenceNumber = lastSequenceNumber + 1;

        var message = new Message(
            request.ConversationId,
            nextSequenceNumber,
            request.SenderUserId,
            request.ClientMessageId,
            request.Body,
            request.ReplyToMessageId,
            replyToSequenceNumber);

        _dbContext.Messages.Add(message);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
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
        });
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