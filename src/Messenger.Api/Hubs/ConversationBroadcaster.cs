using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Hubs;

/// <summary>SignalR group names used by the chat hub.</summary>
public static class RealtimeGroups
{
    /// <summary>Every connection of a user (all devices and tabs) joins this group on connect.</summary>
    public static string User(Guid userId) => $"user:{userId}";

    /// <summary>Connections that have the conversation open (typing, presence, receipts).</summary>
    public static string Conversation(Guid conversationId) => $"conversation:{conversationId}";
}

/// <summary>
/// Sends conversation events to the current members' user groups, so every device of every member is updated
/// (also chats that are not open, e.g. the conversation list) and members who left stop receiving them at once.
/// </summary>
public sealed class ConversationBroadcaster
{
    /// <summary>
    /// Above this size the event goes to the conversation group only (clients that have it open) to keep
    /// fan-out bounded; those clients pick up the rest through sync.
    /// </summary>
    public const int UserGroupFanOutLimit = 500;

    private readonly IHubContext<ChatHub> _hubContext;
    private readonly MessengerDbContext _dbContext;
    private readonly ILogger<ConversationBroadcaster> _logger;

    public ConversationBroadcaster(
        IHubContext<ChatHub> hubContext,
        MessengerDbContext dbContext,
        ILogger<ConversationBroadcaster> logger)
    {
        _hubContext = hubContext;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SendToMembersAsync(
        Guid conversationId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var memberUserIds = await _dbContext.ConversationMembers
                .AsNoTracking()
                .Where(x => x.ConversationId == conversationId && x.LeftAtUtc == null)
                .Select(x => x.UserId)
                .Take(UserGroupFanOutLimit + 1)
                .ToListAsync(cancellationToken);

            var clients = memberUserIds.Count > UserGroupFanOutLimit
                ? _hubContext.Clients.Group(RealtimeGroups.Conversation(conversationId))
                : _hubContext.Clients.Groups(memberUserIds.Select(RealtimeGroups.User).ToList());

            await clients.SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is already saved; clients that miss the push receive it through sync.
            _logger.LogWarning(
                exception,
                "Real-time event {EventName} for conversation {ConversationId} could not be sent.",
                eventName,
                conversationId);
        }
    }
}
