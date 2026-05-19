using Messenger.Application.Messages.DeleteMessage;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageDeleter : IMessageDeleter
{
    private readonly MessengerDbContext _dbContext;

    public MessageDeleter(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeleteMessageResult> DeleteAsync(
        DeleteMessageCommand command,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == command.MessageId, cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.SenderUserId != command.UserId)
            throw new UnauthorizedAccessException("You can only delete your own messages.");

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

        return new DeleteMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.DeletedAtUtc);
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