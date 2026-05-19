using Messenger.Application.Conversations.MarkConversationRead;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationReadMarker : IConversationReadMarker
{
    private readonly MessengerDbContext _dbContext;

    public ConversationReadMarker(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MarkConversationReadResult> MarkAsReadAsync(
        MarkConversationReadCommand command,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.UserId,
                cancellationToken);

        if (member is null)
            throw new InvalidOperationException("User is not a member of this conversation.");

        if (command.LastReadSequenceNumber < member.LastReadSequenceNumber)
            throw new InvalidOperationException("LastReadSequenceNumber cannot move backward.");

        member.UpdateLastReadSequenceNumber(command.LastReadSequenceNumber);

        var messageIds = await _dbContext.Messages
            .Where(x =>
                x.ConversationId == command.ConversationId &&
                x.SequenceNumber <= command.LastReadSequenceNumber)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var deliveries = await _dbContext.MessageDeliveries
            .Where(x =>
                messageIds.Contains(x.MessageId) &&
                x.UserId == command.UserId &&
                x.SeenAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            delivery.MarkSeen();
        }

        var readAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MarkConversationReadResult(
            command.ConversationId,
            command.UserId,
            command.LastReadSequenceNumber,
            deliveries.Count,
            readAtUtc);
    }
}