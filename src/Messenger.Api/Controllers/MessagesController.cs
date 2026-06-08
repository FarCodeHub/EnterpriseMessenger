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
using Messenger.Application.Messages.SearchMessages;
using Messenger.Application.Conversations.MarkConversationRead;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Messenger.Contracts.Realtime;
using Messenger.Application.Messages.ForwardMessage;

namespace Messenger.Api.Controllers;

[Authorize]
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
    private readonly IMessageSearchService _messageSearchService;
    private readonly IConversationReadMarker _conversationReadMarker;
    private readonly IMessageForwarder _messageForwarder;
    public MessagesController(
        MessengerDbContext dbContext,
        IHubContext<ChatHub> hubContext,
        IMessageSender messageSender,
        IMessageEditor messageEditor,
        IMessageDeleter messageDeleter,
        IMessageReader messageReader,
        IMessageSearchService messageSearchService,
        IConversationReadMarker conversationReadMarker,
        IMessageForwarder messageForwarder)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _messageSender = messageSender;
        _messageEditor = messageEditor;
        _messageDeleter = messageDeleter;
        _messageReader = messageReader;
        _messageSearchService = messageSearchService;
        _conversationReadMarker = conversationReadMarker;
        _messageForwarder = messageForwarder;
    }




[HttpPost("forward-batch")]
public async Task<IActionResult> ForwardMessages(
    ForwardMessagesRequest request,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var result = await _messageForwarder.ForwardBatchAsync(
        new ForwardMessagesCommand(
            request.SourceMessageIds,
            currentUserId,
            request.TargetConversationIds,
            request.Comment),
        cancellationToken);

    foreach (var message in result)
    {
        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync(
                RealtimeEvents.MessageCreated,
                new
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
                    message.IsDuplicate,
                    message.Attachments,
                    message.ForwardedFromMessageId,
                    message.ForwardedFromUserId,
                    message.ForwardedFromDisplayName
                },
                cancellationToken);
    }

    var lastMessageByConversation = result
        .GroupBy(x => x.ConversationId)
        .Select(x => x.OrderByDescending(m => m.SequenceNumber).First())
        .ToList();

    foreach (var message in lastMessageByConversation)
    {
        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync(
                RealtimeEvents.ConversationUpdated,
                new ConversationUpdatedEvent(
                    message.ConversationId,
                    message.Id,
                    message.Body,
                    message.SenderUserId,
                    null,
                    message.SequenceNumber,
                    message.CreatedAtUtc),
                cancellationToken);
    }

    return Ok(result);
}




[HttpPost("forward")]
public async Task<IActionResult> ForwardMessage(
    ForwardMessageRequest request,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var result = await _messageForwarder.ForwardAsync(
        new ForwardMessageCommand(
            request.SourceMessageId,
            currentUserId,
            request.TargetConversationIds,
            request.Comment),
        cancellationToken);

    foreach (var message in result)
    {
        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync(
                RealtimeEvents.MessageCreated,
                new
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
                    message.IsDuplicate,
                    message.Attachments,
                    message.ForwardedFromMessageId,
                    message.ForwardedFromUserId,
                    message.ForwardedFromDisplayName
                },
                cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{message.ConversationId}")
            .SendAsync(
                RealtimeEvents.ConversationUpdated,
                new ConversationUpdatedEvent(
                    message.ConversationId,
                    message.Id,
                    message.Body,
                    message.SenderUserId,
                    null,
                    message.SequenceNumber,
                    message.CreatedAtUtc),
                cancellationToken);
    }

    return Ok(result);
}

    [EnableRateLimiting("messages")]
    [HttpPost]
    public async Task<IActionResult> SendMessage(
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
 
        var currentUserId = GetCurrentUserId();
        var command = new SendMessageCommand(
            request.ConversationId,
            currentUserId,
            request.ClientMessageId,
            request.Body,
            request.ReplyToMessageId,
            request.Attachments?.Select(x =>new SendMessageAttachmentCommand(
                   x.Id,
                    x.FileName,
                    x.ContentType,
                    x.Size,
                    x.StoragePath)).ToList());

        var result = await _messageSender.SendAsync(command, cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{result.ConversationId}")
            .SendAsync(RealtimeEvents.MessageCreated, result, cancellationToken);

        if (!result.IsDuplicate)
        {

var sender = await _dbContext.Users
    .Where(x => x.Id == result.SenderUserId)
    .Select(x => new
    {
        x.DisplayName,
        x.UserName
    })
    .FirstOrDefaultAsync(cancellationToken);

var senderUserDisplayName = sender?.DisplayName ?? sender?.UserName;

            await _hubContext.Clients
                .Group($"conversation:{result.ConversationId}")
                .SendAsync(RealtimeEvents.ConversationUpdated, new ConversationUpdatedEvent(
        result.ConversationId,
        result.Id,
        result.Body,
        result.SenderUserId,
        senderUserDisplayName ?? "TEST DISPLAY NAME", 
        result.SequenceNumber,
        result.CreatedAtUtc), cancellationToken);
        }

        return Ok(result);


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

        var result = await _messageReader.GetReplyContextAsync(
            messageId,
            radius,
            cancellationToken);

        return Ok(result);

    }

    [HttpGet("conversation/{conversationId:guid}/search")]
    public async Task<IActionResult> SearchMessages(
        Guid conversationId,
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {

        var result = await _messageSearchService.SearchAsync(
            new SearchMessagesQuery(conversationId, query, take),
            cancellationToken);

        return Ok(result);

    }

    [HttpPost("conversation/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkConversationAsRead(
       Guid conversationId,
       MarkConversationReadRequest request,
       CancellationToken cancellationToken)
    {

        var currentUserId = GetCurrentUserId();
        var result = await _conversationReadMarker.MarkAsReadAsync(
            new MarkConversationReadCommand(
                conversationId,
                currentUserId,
                request.LastReadSequenceNumber),
            cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{conversationId}")
            .SendAsync(RealtimeEvents.ConversationRead, result, cancellationToken);

        return Ok(result);

    }

    [HttpPut("{messageId:guid}")]
    public async Task<IActionResult> EditMessage(
     Guid messageId,
     EditMessageRequest request,
     CancellationToken cancellationToken)
    {
        var result = await _messageEditor.EditAsync(
            new EditMessageCommand(
                messageId,
                GetCurrentUserId(),
                request.Body),
            cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{result.ConversationId}")
            .SendAsync(RealtimeEvents.MessageEdited, result, cancellationToken);

        return Ok(result);
    }



    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(
        Guid messageId,
        CancellationToken cancellationToken)
    {

        var currentUserId = GetCurrentUserId();
        var result = await _messageDeleter.DeleteAsync(
            new DeleteMessageCommand(messageId, currentUserId),
            cancellationToken);

        await _hubContext.Clients
            .Group($"conversation:{result.ConversationId}")
            .SendAsync(RealtimeEvents.MessageDeleted, result, cancellationToken);

        return Ok(result);

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

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("UserId not found in token.");

        return Guid.Parse(userIdValue);
    }


}