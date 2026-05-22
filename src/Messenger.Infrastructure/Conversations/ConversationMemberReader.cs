using Messenger.Application.Conversations.GetMembers;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberReader : IConversationMemberReader
{
    private readonly MessengerDbContext _dbContext;

    public ConversationMemberReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ConversationMemberResult>> GetAsync(
        GetConversationMembersQuery query,
        CancellationToken cancellationToken)
    {
        var isCurrentUserMember = await _dbContext.ConversationMembers
            .AnyAsync(x =>
                x.ConversationId == query.ConversationId &&
                x.UserId == query.CurrentUserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (!isCurrentUserMember)
            throw new UnauthorizedAccessException("You are not a member of this conversation.");

        return await _dbContext.ConversationMembers
            .Where(x =>
                x.ConversationId == query.ConversationId &&
                x.LeftAtUtc == null)
            .Join(
                _dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new ConversationMemberResult(
                    user.Id,
                    user.DisplayName,
                    user.UserName,
                    member.JoinedAtUtc,
                    member.IsMuted,
                    member.IsPinned,
                    member.IsArchived,
                    member.Role.ToString()))
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }
}