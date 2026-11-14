using Messenger.Application.Conversations;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ActiveMemberReader : IActiveMemberReader
{
    private readonly MessengerDbContext _dbContext;

    public ActiveMemberReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ConversationMembers
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.LeftAtUtc == null)
            .Select(x => x.UserId)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
