using Messenger.Application.Auditing;
using Messenger.Application.Conversations.RemoveMember;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberRemover : IConversationMemberRemover
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public ConversationMemberRemover(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<RemoveConversationMemberResult> RemoveAsync(
        RemoveConversationMemberCommand command,
        CancellationToken cancellationToken)
    {
        var currentMember = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.CurrentUserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (currentMember is null ||
            (currentMember.Role != ConversationMemberRole.Owner &&
             currentMember.Role != ConversationMemberRole.Admin))
        {
            throw new UnauthorizedAccessException("Only owner or admin can remove members.");
        }

        var targetMember = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.UserIdToRemove &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (targetMember is null)
            throw new InvalidOperationException("Member not found.");

        if (targetMember.Role == ConversationMemberRole.Owner)
            throw new InvalidOperationException("Owner cannot be removed.");

        if (currentMember.Role == ConversationMemberRole.Admin &&
            targetMember.Role == ConversationMemberRole.Admin)
            throw new UnauthorizedAccessException("Admin cannot remove another admin.");

        targetMember.Leave();
        await _auditLogWriter.WriteAsync(
    "MemberRemoved",
    command.CurrentUserId,
    "Conversation",
    command.ConversationId.ToString(),
    new
    {
        command.UserIdToRemove,
        targetMember.LeftAtUtc
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RemoveConversationMemberResult(
            command.ConversationId,
            command.UserIdToRemove,
            targetMember.LeftAtUtc ?? DateTime.UtcNow);
    }
}