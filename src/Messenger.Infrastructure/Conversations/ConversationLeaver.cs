using Messenger.Application.Conversations.LeaveConversation;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationLeaver : IConversationLeaver
{
    private readonly MessengerDbContext _dbContext;

    public ConversationLeaver(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LeaveConversationResult> LeaveAsync(
        LeaveConversationCommand command,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.UserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (member is null)
            throw new InvalidOperationException("User is not a member of this conversation.");

        if (member.Role == ConversationMemberRole.Owner)
            throw new InvalidOperationException("Owner cannot leave the conversation.");

        member.Leave();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LeaveConversationResult(
            command.ConversationId,
            command.UserId,
            member.LeftAtUtc ?? DateTime.UtcNow);
    }
}