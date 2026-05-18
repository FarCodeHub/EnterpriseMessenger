using Microsoft.AspNetCore.SignalR;

namespace Messenger.Api.Hubs;

public sealed class ChatHub : Hub
{
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"conversation:{conversationId}");
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"conversation:{conversationId}");
    }
}