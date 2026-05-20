using Messenger.Api.Realtime;
using Microsoft.AspNetCore.SignalR;
using Messenger.Infrastructure.Persistence;
using Messenger.Domain.Messages;
using Microsoft.EntityFrameworkCore;
namespace Messenger.Api.Hubs;
using Microsoft.AspNetCore.Authorization;


[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IConnectionTracker _connectionTracker;
    private readonly MessengerDbContext _dbContext;

public ChatHub(
    IConnectionTracker connectionTracker,
    MessengerDbContext dbContext)
{
    _connectionTracker = connectionTracker;
    _dbContext = dbContext;
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

    await EnsureConversationMemberAsync(parsedConversationId, currentUserId);
 
    await Groups.AddToGroupAsync(
        Context.ConnectionId,
        $"conversation:{parsedConversationId}");

    await Clients.Caller.SendAsync("conversation.joined", new
    {
        ConversationId = parsedConversationId
    });
}

 public async Task LeaveConversation(string conversationId)
{
    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new HubException("Invalid conversationId.");

    await Groups.RemoveFromGroupAsync(
        Context.ConnectionId,
        $"conversation:{parsedConversationId}");
}
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
      var userId = await _connectionTracker.RemoveConnectionAsync(Context.ConnectionId);

        if (userId is not null)
        {
            await Clients.All.SendAsync("presence.offline", new
            {
                UserId = userId.Value
            });
        }

        await base.OnDisconnectedAsync(exception);
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
 
public async Task StartTyping(string conversationId)
{
    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new HubException("Invalid conversationId.");

    var currentUserId = GetCurrentUserId();

    await EnsureConversationMemberAsync(parsedConversationId, currentUserId);

    await Clients
        .GroupExcept($"conversation:{parsedConversationId}", Context.ConnectionId)
        .SendAsync("typing.started", new
        {
            ConversationId = parsedConversationId,
            UserId = currentUserId,
            StartedAtUtc = DateTime.UtcNow
        });
}

public async Task StopTyping(string conversationId)
{
    if (!Guid.TryParse(conversationId, out var parsedConversationId))
        throw new HubException("Invalid conversationId.");

    var currentUserId = GetCurrentUserId();

    await EnsureConversationMemberAsync(parsedConversationId, currentUserId);

    await Clients
        .GroupExcept($"conversation:{parsedConversationId}", Context.ConnectionId)
        .SendAsync("typing.stopped", new
        {
            ConversationId = parsedConversationId,
            UserId = currentUserId,
            StoppedAtUtc = DateTime.UtcNow
        });
}

private Guid GetCurrentUserId()
{
    var userIdValue = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrWhiteSpace(userIdValue))
        throw new HubException("UserId not found in token.");

    return Guid.Parse(userIdValue);
}


private async Task EnsureConversationMemberAsync(
    Guid conversationId,
    Guid userId)
{
    var isMember = await _dbContext.ConversationMembers
        .AnyAsync(x =>
            x.ConversationId == conversationId &&
            x.UserId == userId);

    if (!isMember)
        throw new HubException("You are not a member of this conversation.");
}

}