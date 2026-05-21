using Messenger.Application.Conversations.SetMemberRole;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberRoleSetter : IConversationMemberRoleSetter
{
    private readonly MessengerDbContext _dbContext;

    public ConversationMemberRoleSetter(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SetConversationMemberRoleResult> SetAsync(
        SetConversationMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        var currentMember = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.CurrentUserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (currentMember is null || currentMember.Role != ConversationMemberRole.Owner)
            throw new UnauthorizedAccessException("Only owner can change member roles.");

        var targetMember = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.TargetUserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (targetMember is null)
            throw new InvalidOperationException("Member not found.");

        if (targetMember.Role == ConversationMemberRole.Owner)
            throw new InvalidOperationException("Owner role cannot be changed.");

        if (!Enum.IsDefined(typeof(ConversationMemberRole), command.Role))
            throw new InvalidOperationException("Invalid role.");

        var newRole = (ConversationMemberRole)command.Role;

        if (newRole == ConversationMemberRole.Owner)
            throw new InvalidOperationException("Cannot promote another member to owner.");

        if (newRole == ConversationMemberRole.Admin)
            targetMember.PromoteToAdmin();
        else if (newRole == ConversationMemberRole.Member)
            targetMember.DemoteToMember();
        else
            throw new InvalidOperationException("Invalid role.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetConversationMemberRoleResult(
            command.ConversationId,
            command.TargetUserId,
            targetMember.Role.ToString());
    }
}