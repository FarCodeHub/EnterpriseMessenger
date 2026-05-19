using Messenger.Contracts.Messages;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Messenger.Application.Messages.SendMessage;
using Messenger.Application.Messages.EditMessage;
using Messenger.Application.Messages.DeleteMessage;
using Messenger.Application.Messages.GetMessages;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class MessagesController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IMessageSender _messageSender;
    private readonly IMessageEditor _messageEditor;
    private readonly IMessageDeleter _messageDeleter;
    private readonly IMessageReader _messageReader;

    public MessagesController(
        MessengerDbContext dbContext,
        IHubContext<ChatHub> hubContext,
        IMessageSender messageSender,
        IMessageEditor messageEditor,
        IMessageDeleter messageDeleter,
        IMessageReader messageReader)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _messageSender = messageSender;
        _messageEditor = messageEditor;
        _messageDeleter = messageDeleter;
        _messageReader = messageReader;
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
        var result = await _messageReader.GetConversationMessagesAsync(
            conversationId,
            beforeSequence,
            take,
            cancellationToken);

        return Ok(result);
    }


    [HttpGet("conversation/{conversationId:guid}/around/{sequenceNumber:long}")]
    public async Task<IActionResult> GetMessagesAround(
        Guid conversationId,
        long sequenceNumber,
        int radius = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageReader.GetMessagesAroundAsync(
            conversationId,
            sequenceNumber,
            radius,
            cancellationToken);

        return Ok(result);
    }


    [HttpGet("{messageId:guid}/reply-context")]
    public async Task<IActionResult> GetReplyContext(
       Guid messageId,
       int radius = 100,
       CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _messageReader.GetReplyContextAsync(
                messageId,
                radius,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
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
        try
        {
            var result = await _messageEditor.EditAsync(
                new EditMessageCommand(
                    messageId,
                    request.UserId,
                    request.Body),
                cancellationToken);

            await _hubContext.Clients
                .Group($"conversation:{result.ConversationId}")
                .SendAsync("message.edited", result, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _messageDeleter.DeleteAsync(
                new DeleteMessageCommand(messageId, userId),
                cancellationToken);

            await _hubContext.Clients
                .Group($"conversation:{result.ConversationId}")
                .SendAsync("message.deleted", result, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
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