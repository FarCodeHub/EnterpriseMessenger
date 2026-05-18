using Messenger.Api.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.Api.Hubs;

public sealed class ChatHub : Hub
{
    private readonly InMemoryConnectionTracker _connectionTracker;

    public ChatHub(InMemoryConnectionTracker connectionTracker)
    {
        _connectionTracker = connectionTracker;
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
}