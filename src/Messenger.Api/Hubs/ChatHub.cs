using Messenger.Api.Realtime;
using Microsoft.AspNetCore.SignalR;
using Messenger.Infrastructure.Persistence;
using Messenger.Domain.Messages;
using Microsoft.EntityFrameworkCore;
namespace Messenger.Api.Hubs;

public sealed class ChatHub : Hub
{
    private readonly InMemoryConnectionTracker _connectionTracker;
    private readonly MessengerDbContext _dbContext;

    public ChatHub(
        InMemoryConnectionTracker connectionTracker,
        MessengerDbContext dbContext)
    {
        _connectionTracker = connectionTracker;
        _dbContext = dbContext;
    }

    public async Task RegisterUser(string userId)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new HubException("Invalid userId.");

        _connectionTracker.AddConnection(parsedUserId, Context.ConnectionId);

        await Clients.Caller.SendAsync("presence.registered", new
        {
            UserId = parsedUserId,
            Context.ConnectionId
        });
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"conversation:{conversationId}");

        await Clients.Caller.SendAsync("conversation.joined", new
        {
            ConversationId = conversationId
        });
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"conversation:{conversationId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _connectionTracker.RemoveConnection(Context.ConnectionId);

        if (userId is not null)
        {
            await Clients.All.SendAsync("presence.offline", new
            {
                UserId = userId.Value
            });
        }

        await base.OnDisconnectedAsync(exception);
    }


    public async Task AcknowledgeDelivered(string messageId, string userId)
    {
        if (!Guid.TryParse(messageId, out var parsedMessageId))
            throw new HubException("Invalid messageId.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new HubException("Invalid userId.");

        var delivery = await _dbContext.MessageDeliveries
            .FirstOrDefaultAsync(x =>
                x.MessageId == parsedMessageId &&
                x.UserId == parsedUserId);

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
                    ConversationId = message.ConversationId,
                    SequenceNumber = message.SequenceNumber,
                    DeliveredByUserId = parsedUserId,
                    DeliveredAtUtc = DateTime.UtcNow
                });
        }


        await Clients.Caller.SendAsync("message.delivered.ack", new
        {
            MessageId = parsedMessageId,
            UserId = parsedUserId,
            DeliveredAtUtc = DateTime.UtcNow
        });
    }



    public async Task AcknowledgeSeen(string messageId, string userId)
    {
        if (!Guid.TryParse(messageId, out var parsedMessageId))
            throw new HubException("Invalid messageId.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new HubException("Invalid userId.");

        var delivery = await _dbContext.MessageDeliveries
            .FirstOrDefaultAsync(x =>
                x.MessageId == parsedMessageId &&
                x.UserId == parsedUserId);

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
                    ConversationId = message.ConversationId,
                    SequenceNumber = message.SequenceNumber,
                    SeenByUserId = parsedUserId,
                    SeenAtUtc = DateTime.UtcNow
                });
        }


        await Clients.Caller.SendAsync("message.seen.ack", new
        {
            MessageId = parsedMessageId,
            UserId = parsedUserId,
            SeenAtUtc = DateTime.UtcNow
        });
    }


    public async Task StartTyping(string conversationId, string userId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversationId.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new HubException("Invalid userId.");

        await Clients
            .GroupExcept($"conversation:{parsedConversationId}", Context.ConnectionId)
            .SendAsync("typing.started", new
            {
                ConversationId = parsedConversationId,
                UserId = parsedUserId,
                StartedAtUtc = DateTime.UtcNow
            });
    }

    public async Task StopTyping(string conversationId, string userId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversationId.");

        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new HubException("Invalid userId.");

        await Clients
            .GroupExcept($"conversation:{parsedConversationId}", Context.ConnectionId)
            .SendAsync("typing.stopped", new
            {
                ConversationId = parsedConversationId,
                UserId = parsedUserId,
                StoppedAtUtc = DateTime.UtcNow
            });
    }



}