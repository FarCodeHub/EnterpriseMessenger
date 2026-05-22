using Messenger.Application.Auditing;
using Messenger.Application.Conversations;
using Messenger.Application.Conversations.RemoveMember;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberRemover : IConversationMemberRemover
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IConversationPermissionChecker _permissionChecker;

    public ConversationMemberRemover(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter, IConversationPermissionChecker permissionChecker)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
        _permissionChecker = permissionChecker;
    }

    public async Task<RemoveConversationMemberResult> RemoveAsync(
        RemoveConversationMemberCommand command,
        CancellationToken cancellationToken)
    {




        var currentUserRole = await _permissionChecker.GetActiveMemberRoleAsync(
         command.ConversationId,
         command.CurrentUserId,
         cancellationToken);



        await _permissionChecker.EnsureOwnerOrAdminAsync(
            command.ConversationId,
            command.CurrentUserId,
            cancellationToken);


        var targetMember = await _dbContext.ConversationMembers
          .FirstOrDefaultAsync(x =>
              x.ConversationId == command.ConversationId &&
              x.UserId == command.UserIdToRemove &&
              x.LeftAtUtc == null,
              cancellationToken);




        if (targetMember is null)
            throw new InvalidOperationException("User is not an active member of the conversation.");


if (targetMember.Role == ConversationMemberRole.Owner)
    throw new InvalidOperationException("Owner cannot be removed.");
    
        if (currentUserRole == ConversationMemberRole.Admin &&
            targetMember.Role == ConversationMemberRole.Admin)
        {
            throw new UnauthorizedAccessException("Admin cannot remove another admin.");
        }




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