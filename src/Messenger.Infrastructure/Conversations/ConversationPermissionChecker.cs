using Messenger.Application.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationPermissionChecker : IConversationPermissionChecker
{
    private readonly MessengerDbContext _dbContext;

    public ConversationPermissionChecker(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureActiveMemberAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ConversationMembers
            .AnyAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == userId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (!exists)
            throw new UnauthorizedAccessException("You are not a member of this conversation.");
    }

    public async Task EnsureOwnerOrAdminAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == userId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (member is null ||
            (member.Role != ConversationMemberRole.Owner &&
             member.Role != ConversationMemberRole.Admin))
        {
            throw new UnauthorizedAccessException("Only owner or admin can perform this action.");
        }
    }

    public async Task EnsureOwnerAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isOwner = await _dbContext.ConversationMembers
            .AnyAsync(x =>
                x.ConversationId == conversationId &&
                x.UserId == userId &&
                x.LeftAtUtc == null &&
                x.Role == ConversationMemberRole.Owner,
                cancellationToken);

        if (!isOwner)
            throw new UnauthorizedAccessException("Only owner can perform this action.");
    }
}