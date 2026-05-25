
using Microsoft.AspNetCore.SignalR;
using Messenger.Infrastructure.Persistence;
using Messenger.Domain.Messages;
using Microsoft.EntityFrameworkCore;
namespace Messenger.Api.Hubs;

using Messenger.Application.Abstractions.Realtime;
using Messenger.Application.Conversations;
using Messenger.Application.Realtime;
using Microsoft.AspNetCore.Authorization;


[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IConnectionTracker _connectionTracker;
    private readonly MessengerDbContext _dbContext;
    private readonly IPresenceService _presenceService;
    private readonly ITypingService _typingService;
    private readonly IReadReceiptService _readReceiptService;
    private readonly IConversationPermissionChecker _permissionChecker;
    private readonly IDeliveryReceiptService _deliveryReceiptService;

    public ChatHub(
        IConnectionTracker connectionTracker,
        MessengerDbContext dbContext,
        IConversationPermissionChecker permissionChecker,
        IPresenceService presenceService,
        ITypingService typingService,
        IReadReceiptService readReceiptService,
        IDeliveryReceiptService deliveryReceiptService)
    {
        _connectionTracker = connectionTracker;
        _dbContext = dbContext;
        _permissionChecker = permissionChecker;
        _presenceService = presenceService;
        _typingService = typingService;
        _readReceiptService = readReceiptService;
        _deliveryReceiptService = deliveryReceiptService;
    }


    public async Task StartTyping(string conversationId)
{
    var userId = GetCurrentUserId();

    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new InvalidOperationException("Invalid conversation id.");

await _typingService.StartTypingAsync(
    parsedConversationId,
    userId,
    Context.ConnectionAborted);

    await Clients
        .OthersInGroup($"conversation:{parsedConversationId}")
        .SendAsync(
            "typing.started",
            new
            {
                ConversationId = parsedConversationId,
                UserId = userId
            },
            Context.ConnectionAborted);
}

public async Task StopTyping(string conversationId)
{
    var userId = GetCurrentUserId();

    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new InvalidOperationException("Invalid conversation id.");

  await _typingService.StartTypingAsync(
    parsedConversationId,
    userId,
    Context.ConnectionAborted);

    await Clients
        .OthersInGroup($"conversation:{parsedConversationId}")
        .SendAsync(
            "typing.stopped",
            new
            {
                ConversationId = parsedConversationId,
                UserId = userId
            },
            Context.ConnectionAborted);
}

    public async Task RegisterUser()
    {
        var userId = GetCurrentUserId();

        await _connectionTracker.AddConnectionAsync(userId, Context.ConnectionId);

        await Clients.Caller.SendAsync("presence.registered", new
        {
            UserId = userId,
            Context.ConnectionId
        });
    }

    public async Task JoinConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversationId.");

        var currentUserId = GetCurrentUserId();

        await _permissionChecker.EnsureActiveMemberAsync(
        parsedConversationId,
        currentUserId,
        Context.ConnectionAborted);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"conversation:{parsedConversationId}");

        await Clients.Caller.SendAsync("conversation.joined", new
        {
            ConversationId = parsedConversationId
        });
    }


 public override async Task OnConnectedAsync()
{
    var userId = GetCurrentUserId();

    await _presenceService.SetOnlineAsync(
        userId,
        Context.ConnectionId,
        Context.ConnectionAborted);

    await Clients.All.SendAsync(
        "presence.online",
        userId,
        Context.ConnectionAborted);

    await base.OnConnectedAsync();
}


public override async Task OnDisconnectedAsync(Exception? exception)
{
    var userId = GetCurrentUserId();

    var isOffline = await _presenceService.RemoveConnectionAsync(
        userId,
        Context.ConnectionId,
        Context.ConnectionAborted);

    if (isOffline)
    {
        await Clients.All.SendAsync(
            "presence.offline",
            userId,
            Context.ConnectionAborted);
    }

    await base.OnDisconnectedAsync(exception);
}

    public async Task LeaveConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversationId.");

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"conversation:{parsedConversationId}");
    }
 


    public async Task AcknowledgeDelivered(string messageId)
    {
        if (!Guid.TryParse(messageId, out var parsedMessageId))
            throw new HubException("Invalid messageId.");

        var currentUserId = GetCurrentUserId();

        var delivery = await _dbContext.MessageDeliveries
            .FirstOrDefaultAsync(x =>
                x.MessageId == parsedMessageId &&
                x.UserId == currentUserId);

        if (delivery is null)
            return;

        delivery.MarkDelivered();

        await _dbContext.SaveChangesAsync();

        var message = await _dbContext.Messages
            .Where(x => x.Id == parsedMessageId)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.SenderUserId,
                x.SequenceNumber
            })
            .FirstOrDefaultAsync();

        if (message is not null)
        {
            await Clients
                .Group($"conversation:{message.ConversationId}")
                .SendAsync("message.delivered", new
                {
                    MessageId = message.Id,

                    message.ConversationId,
                    message.SequenceNumber,
                    DeliveredByUserId = currentUserId,
                    DeliveredAtUtc = DateTime.UtcNow
                });
        }

        await Clients.Caller.SendAsync("message.delivered.ack", new
        {
            MessageId = parsedMessageId,
            UserId = currentUserId,
            DeliveredAtUtc = DateTime.UtcNow
        });
    }


    public async Task AcknowledgeSeen(string messageId)
    {
        if (!Guid.TryParse(messageId, out var parsedMessageId))
            throw new HubException("Invalid messageId.");

        var currentUserId = GetCurrentUserId();

        var delivery = await _dbContext.MessageDeliveries
            .FirstOrDefaultAsync(x =>
                x.MessageId == parsedMessageId &&
                x.UserId == currentUserId);

        if (delivery is null)
            return;

        delivery.MarkSeen();

        await _dbContext.SaveChangesAsync();

        var message = await _dbContext.Messages
            .Where(x => x.Id == parsedMessageId)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.SenderUserId,
                x.SequenceNumber
            })
            .FirstOrDefaultAsync();

        if (message is not null)
        {
            await Clients
                .Group($"conversation:{message.ConversationId}")
                .SendAsync("message.seen", new
                {
                    MessageId = message.Id,
                    message.ConversationId,
                    message.SequenceNumber,
                    SeenByUserId = currentUserId,
                    SeenAtUtc = DateTime.UtcNow
                });
        }

        await Clients.Caller.SendAsync("message.seen.ack", new
        {
            MessageId = parsedMessageId,
            UserId = currentUserId,
            SeenAtUtc = DateTime.UtcNow
        });
    }



    private Guid GetCurrentUserId()
    {
        var userIdValue = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new HubException("UserId not found in token.");

        return Guid.Parse(userIdValue);
    }



public async Task MarkAsDelivered(
    string conversationId,
    long sequenceNumber)
{
    var userId = GetCurrentUserId();

    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new InvalidOperationException("Invalid conversation id.");

await _deliveryReceiptService.MarkAsDeliveredAsync(
    parsedConversationId,
    userId,
    sequenceNumber,
    Context.ConnectionAborted);

await Clients
    .OthersInGroup($"conversation:{parsedConversationId}")
    .SendAsync(
        "message.delivered",
        new
        {
            ConversationId = parsedConversationId,
            UserId = userId,
            SequenceNumber = sequenceNumber
        },
        Context.ConnectionAborted);
}



public async Task MarkAsSeen(
    string conversationId,
    long sequenceNumber)
{
    var userId = GetCurrentUserId();

    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new InvalidOperationException("Invalid conversation id.");

   await _readReceiptService.MarkAsSeenAsync(
    parsedConversationId,
    userId,
    sequenceNumber,
    Context.ConnectionAborted);

await Clients
    .OthersInGroup($"conversation:{parsedConversationId}")
    .SendAsync(
        "message.seen",
        new
        {
            ConversationId = parsedConversationId,
            UserId = userId,
            SequenceNumber = sequenceNumber
        },
        Context.ConnectionAborted);
}


}