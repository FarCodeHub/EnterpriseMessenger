using Messenger.Contracts.Messages;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Messenger.Application.Messages.SendMessage;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class MessagesController : ControllerBase
{
private readonly MessengerDbContext _dbContext;
private readonly IHubContext<ChatHub> _hubContext;
private readonly IMessageSender _messageSender;

public MessagesController(
    MessengerDbContext dbContext,
    IHubContext<ChatHub> hubContext,
    IMessageSender messageSender)
{
    _dbContext = dbContext;
    _hubContext = hubContext;
    _messageSender = messageSender;
}

   [HttpPost]
public async Task<IActionResult> SendMessage(
    SendMessageRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var command = new SendMessageCommand(
            request.ConversationId,
            request.SenderUserId,
            request.ClientMessageId,
            request.Body,
            request.ReplyToMessageId,
            request.Attachments?.Select(x =>
                new SendMessageAttachmentCommand(
                    x.FileName,
                    x.ContentType,
                    x.Size,
                    x.StoragePath)).ToList());

        var result = await _messageSender.SendAsync(command, cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{result.ConversationId}")
            .SendAsync("message.received", result, cancellationToken);

        if (!result.IsDuplicate)
        {
            await _hubContext.Clients
                .Group($"conversation:{result.ConversationId}")
                .SendAsync("conversation.updated", new
                {
                    ConversationId = result.ConversationId,
                    LastMessageId = result.Id,
                    LastMessageBody = result.Body,
                    LastMessageAtUtc = result.CreatedAtUtc,
                    LastMessageSenderUserId = result.SenderUserId,
                    LastMessageSequenceNumber = result.SequenceNumber
                }, cancellationToken);
        }

        return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
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
                x.CreatedAtUtc,
                Attachments = _dbContext.Attachments
        .Where(a => a.MessageId == x.Id)
        .Select(a => new
        {
            a.Id,
            a.FileName,
            a.ContentType,
            a.Size,
            a.StoragePath
        })
        .ToList()
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
                x.CreatedAtUtc,
                Attachments = _dbContext.Attachments
        .Where(a => a.MessageId == x.Id)
        .Select(a => new
        {
            a.Id,
            a.FileName,
            a.ContentType,
            a.Size,
            a.StoragePath
        })
        .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }


    [HttpGet("{messageId:guid}/reply-context")]
    public async Task<IActionResult> GetReplyContext(
        Guid messageId,
        int radius = 100,
        CancellationToken cancellationToken = default)
    {
        radius = Math.Clamp(radius, 10, 200);

        var message = await _dbContext.Messages
            .Where(x => x.Id == messageId)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
            return NotFound("Message not found.");

        if (message.ReplyToSequenceNumber is null)
            return BadRequest("This message is not a reply.");

        var from = message.ReplyToSequenceNumber.Value - radius;
        var to = message.ReplyToSequenceNumber.Value + radius;

        var messages = await _dbContext.Messages
            .Where(x =>
                x.ConversationId == message.ConversationId &&
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
                x.CreatedAtUtc,
                Attachments = _dbContext.Attachments
        .Where(a => a.MessageId == x.Id)
        .Select(a => new
        {
            a.Id,
            a.FileName,
            a.ContentType,
            a.Size,
            a.StoragePath
        })
        .ToList(),
                IsTarget = x.Id == message.ReplyToMessageId
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            message.Id,
            message.ConversationId,
            TargetMessageId = message.ReplyToMessageId,
            TargetSequenceNumber = message.ReplyToSequenceNumber,
            Radius = radius,
            Messages = messages
        });
    }

    [HttpGet("conversation/{conversationId:guid}/search")]
    public async Task<IActionResult> SearchMessages(
        Guid conversationId,
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query is required.");

        take = Math.Clamp(take, 1, 50);

        var messages = await _dbContext.Messages
            .Where(x =>
                x.ConversationId == conversationId &&
                EF.Functions.ILike(x.Body, $"%{query}%"))
            .OrderByDescending(x => x.SequenceNumber)
            .Take(take)
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

        return Ok(new
        {
            ConversationId = conversationId,
            Query = query,
            Count = messages.Count,
            Results = messages
        });
    }


    [HttpPost("conversation/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkConversationAsRead(
        Guid conversationId,
        MarkConversationReadRequest request,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == request.UserId,
                cancellationToken);

        if (member is null)
            return BadRequest("User is not a member of this conversation.");

        if (request.LastReadSequenceNumber < member.LastReadSequenceNumber)
            return BadRequest("LastReadSequenceNumber cannot move backward.");

        member.UpdateLastReadSequenceNumber(request.LastReadSequenceNumber);

        var messageIds = await _dbContext.Messages
            .Where(x =>
                x.ConversationId == conversationId &&
                x.SequenceNumber <= request.LastReadSequenceNumber)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var deliveries = await _dbContext.MessageDeliveries
            .Where(x =>
                messageIds.Contains(x.MessageId) &&
                x.UserId == request.UserId &&
                x.SeenAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            delivery.MarkSeen();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{conversationId}")
            .SendAsync("conversation.read", new
            {
                ConversationId = conversationId,
                request.UserId,
                request.LastReadSequenceNumber,
                SeenMessagesCount = deliveries.Count,
                ReadAtUtc = DateTime.UtcNow
            }, cancellationToken);

        return Ok(new
        {
            ConversationId = conversationId,
            request.UserId,
            request.LastReadSequenceNumber,
            SeenMessagesCount = deliveries.Count
        });
    }

    [HttpPut("{messageId:guid}")]
    public async Task<IActionResult> EditMessage(
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message is null)
            return NotFound("Message not found.");

        if (message.SenderUserId != request.UserId)
            return StatusCode(StatusCodes.Status403Forbidden, "You can only edit your own messages.");

        message.Edit(request.Body);
        await AddSyncEventForConversationMembers(
            message.ConversationId,
            message.Id,
            "MessageEdited",
            new
            {
                MessageId = message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.Body,
                message.EditedAtUtc
            },
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new
        {
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.SenderUserId,
            message.Body,
            message.EditedAtUtc
        };

        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync("message.edited", response, cancellationToken);

        return Ok(response);
    }
    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message is null)
            return NotFound("Message not found.");

        if (message.SenderUserId != userId)
            return StatusCode(StatusCodes.Status403Forbidden, "You can only delete your own messages.");

        message.Delete();
        await AddSyncEventForConversationMembers(
            message.ConversationId,
            message.Id,
            "MessageDeleted",
            new
            {
                MessageId = message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.DeletedAtUtc
            },
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new
        {
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.DeletedAtUtc
        };

        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync("message.deleted", response, cancellationToken);

        return Ok(response);
    }


    private async Task AddSyncEventForConversationMembers(
        Guid conversationId,
        Guid? messageId,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        var memberUserIds = await _dbContext.ConversationMembers
            .Where(x => x.ConversationId == conversationId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);

        foreach (var memberUserId in memberUserIds)
        {
            var syncCounter = await _dbContext.UserSyncCounters
                .FirstOrDefaultAsync(x => x.UserId == memberUserId, cancellationToken);

            if (syncCounter is null)
            {
                syncCounter = new Messenger.Domain.Sync.UserSyncCounter(memberUserId);
                _dbContext.UserSyncCounters.Add(syncCounter);
            }

            var eventId = syncCounter.GetNextEventId();

            _dbContext.UserSyncEvents.Add(
                new Messenger.Domain.Sync.UserSyncEvent(
                    eventId,
                    memberUserId,
                    eventType,
                    payloadJson,
                    conversationId,
                    messageId));
        }
    }





}