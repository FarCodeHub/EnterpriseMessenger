using Messenger.Application.Auditing;
using Messenger.Application.Conversations;
using Messenger.Application.Conversations.SetMemberRole;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberRoleSetter : IConversationMemberRoleSetter
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IConversationPermissionChecker _permissionChecker;

    public ConversationMemberRoleSetter(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter, IConversationPermissionChecker permissionChecker)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _permissionChecker = permissionChecker;
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

        await _permissionChecker.EnsureOwnerAsync(
    command.ConversationId,
    command.CurrentUserId,
    cancellationToken);

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


            await _auditLogWriter.WriteAsync(
    "MemberRoleChanged",
    command.CurrentUserId,
    "Conversation",
    command.ConversationId.ToString(),
    new
    {
        command.TargetUserId,
        NewRole = targetMember.Role.ToString()
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetConversationMemberRoleResult(
            command.ConversationId,
            command.TargetUserId,
            targetMember.Role.ToString());
    }
}