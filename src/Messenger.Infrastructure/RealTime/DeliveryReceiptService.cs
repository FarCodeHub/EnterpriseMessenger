using Messenger.Application.Conversations;
using Messenger.Application.Realtime;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Realtime;

public sealed class DeliveryReceiptService : IDeliveryReceiptService
{
    private readonly IConversationPermissionChecker _permissionChecker;
    private readonly MessengerDbContext _dbContext;
    public DeliveryReceiptService(
        IConversationPermissionChecker permissionChecker, MessengerDbContext dbContext)
    {
        _permissionChecker = permissionChecker;
        _dbContext = dbContext;
    }

    public async Task MarkAsDeliveredAsync(
        Guid conversationId,
        Guid userId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        await _permissionChecker.EnsureActiveMemberAsync(
            conversationId,
            userId,
            cancellationToken);

        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == userId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (member is null)
            throw new UnauthorizedAccessException("User is not an active member of this conversation.");

        member.MarkAsDelivered(sequenceNumber);


var lastSequenceNumber = await _dbContext.Messages
    .Where(x => x.ConversationId == conversationId)
    .MaxAsync(x => (long?)x.SequenceNumber, cancellationToken) ?? 0;

if (sequenceNumber > lastSequenceNumber)
    throw new InvalidOperationException("Invalid sequence number.");


        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}