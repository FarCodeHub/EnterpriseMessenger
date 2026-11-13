
using Microsoft.AspNetCore.SignalR;
using Messenger.Infrastructure.Persistence;
using Messenger.Domain.Messages;
using Microsoft.EntityFrameworkCore;
namespace Messenger.Api.Hubs;

using System.Threading.RateLimiting;
using Messenger.Application.Abstractions.Realtime;
using Messenger.Application.Conversations;
using Messenger.Application.Realtime;
using Messenger.Contracts.Realtime;
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

        await _permissionChecker.EnsureActiveMemberAsync(
            parsedConversationId,
            userId,
            Context.ConnectionAborted);

        await _typingService.StartTypingAsync(
            parsedConversationId,
            userId,
            Context.ConnectionAborted);

        await Clients
            .OthersInGroup($"conversation:{parsedConversationId}")
            .SendAsync(
                RealtimeEvents.TypingStarted,
        new TypingEvent(parsedConversationId, userId),
                Context.ConnectionAborted);
    }

    public async Task StopTyping(string conversationId)
    {
        var userId = GetCurrentUserId();

        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new InvalidOperationException("Invalid conversation id.");

        await _permissionChecker.EnsureActiveMemberAsync(
            parsedConversationId,
            userId,
            Context.ConnectionAborted);

        await _typingService.StopTypingAsync(
          parsedConversationId,
          userId,
          Context.ConnectionAborted);

        await Clients
            .OthersInGroup($"conversation:{parsedConversationId}")
            .SendAsync(
                RealtimeEvents.TypingStopped,
              new TypingEvent(parsedConversationId, userId),
                Context.ConnectionAborted);
    }

    public async Task RegisterUser()
    {
        var userId = GetCurrentUserId();

        await _connectionTracker.AddConnectionAsync(userId, Context.ConnectionId);

        await Clients.Caller.SendAsync(RealtimeEvents.PresenceRegistered, new
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

        await _presenceService.JoinConversationAsync(
 parsedConversationId,
 currentUserId,
 Context.ConnectionId,
 Context.ConnectionAborted);



        var onlineUserIds = await _presenceService.GetOnlineConversationUserIdsAsync(
        parsedConversationId,
        Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
             RealtimeEvents.PresenceSnapshot,
            new
            {
                ConversationId = parsedConversationId,
                UserIds = onlineUserIds
            },
            Context.ConnectionAborted);



        await Clients
        .OthersInGroup($"conversation:{parsedConversationId}")
        .SendAsync(RealtimeEvents.PresenceChanged
            ,
            new
            {
                ConversationId = parsedConversationId,
                UserId = currentUserId,
                IsOnline = true
            },
            Context.ConnectionAborted);

        await Clients.Caller.SendAsync(RealtimeEvents.ConversationJoined, new
        {
            ConversationId = parsedConversationId
        });
    }


    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();

        // Every device of the user receives its conversations' messages, also for chats that are not open.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.User(userId),
            Context.ConnectionAborted);

        await _presenceService.SetOnlineAsync(
            userId,
            Context.ConnectionId,
            Context.ConnectionAborted);

        // await Clients.All.SendAsync(
        //     RealtimeEvents.PresenceOnline,
        //     userId,
        //     Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();

        IReadOnlyCollection<Guid> conversationIds;

        try
        {
            conversationIds = await _presenceService.GetConnectionConversationIdsAsync(
                Context.ConnectionId,
                Context.ConnectionAborted);
        }
        catch
        {
            conversationIds = Array.Empty<Guid>();
        }

        foreach (var conversationId in conversationIds)
        {
            var isOfflineInConversation = await _presenceService.LeaveConversationAsync(
                conversationId,
                userId,
                Context.ConnectionId,
                Context.ConnectionAborted);

            if (!isOfflineInConversation)
                continue;

            await Clients
                .Group($"conversation:{conversationId}")
                .SendAsync(
                    RealtimeEvents.PresenceChanged,
                    new
                    {
                        ConversationId = conversationId,
                        UserId = userId,
                        IsOnline = false
                    },
                    CancellationToken.None);
        }

        await _presenceService.RemoveConnectionAsync(
            userId,
            Context.ConnectionId,
            CancellationToken.None);

        await base.OnDisconnectedAsync(exception);
    }
    public async Task LeaveConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversationId.");

        var currentUserId = GetCurrentUserId();

        var groupName = $"conversation:{parsedConversationId}";

        var isOfflineInConversation = await _presenceService.LeaveConversationAsync(
            parsedConversationId,
            currentUserId,
            Context.ConnectionId,
            Context.ConnectionAborted);

        if (isOfflineInConversation)
        {
            await Clients
                .OthersInGroup(groupName)
                .SendAsync(
                    RealtimeEvents.PresenceChanged,
                    new
                    {
                        ConversationId = parsedConversationId,
                        UserId = currentUserId,
                        IsOnline = false
                    },
                    Context.ConnectionAborted);
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            groupName,
            Context.ConnectionAborted);
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
                .SendAsync(RealtimeEvents.MessageDelivered, new
                {
                    MessageId = message.Id,

                    message.ConversationId,
                    message.SequenceNumber,
                    DeliveredByUserId = currentUserId,
                    DeliveredAtUtc = DateTime.UtcNow
                });
        }

        await Clients.Caller.SendAsync(RealtimeEvents.MessageDeliveredACK, new
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
                .SendAsync(RealtimeEvents.MessageSeen, new
                {
                    MessageId = message.Id,
                    message.ConversationId,
                    message.SequenceNumber,
                    SeenByUserId = currentUserId,
                    SeenAtUtc = DateTime.UtcNow
                });
        }

        await Clients.Caller.SendAsync(RealtimeEvents.MessageSeenACK, new
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
                RealtimeEvents.MessageDelivered,
                new MessageReceiptEvent(parsedConversationId, userId, sequenceNumber),
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
                RealtimeEvents.MessageSeen,
            new MessageReceiptEvent(parsedConversationId, userId, sequenceNumber),
                Context.ConnectionAborted);
    }


    public async Task RenewPresence(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversationId.");

        var currentUserId = GetCurrentUserId();

        await _permissionChecker.EnsureActiveMemberAsync(
            parsedConversationId,
            currentUserId,
            Context.ConnectionAborted);

        await _presenceService.RenewConversationPresenceAsync(
            parsedConversationId,
            currentUserId,
            Context.ConnectionId,
            Context.ConnectionAborted);
    }

}