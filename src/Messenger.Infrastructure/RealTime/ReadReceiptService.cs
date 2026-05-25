using Messenger.Application.Conversations;
using Messenger.Application.Realtime;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Realtime;

public sealed class ReadReceiptService : IReadReceiptService
{
    private readonly MessengerDbContext _dbContext;
    private readonly IConversationPermissionChecker _permissionChecker;

    public ReadReceiptService(
        MessengerDbContext dbContext,
        IConversationPermissionChecker permissionChecker)
    {
        _dbContext = dbContext;
        _permissionChecker = permissionChecker;
    }

    public async Task MarkAsSeenAsync(
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
            throw new InvalidOperationException("Conversation member not found.");

        member.MarkAsRead(sequenceNumber);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}