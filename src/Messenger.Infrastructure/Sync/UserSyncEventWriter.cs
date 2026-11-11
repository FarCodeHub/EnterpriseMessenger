using Messenger.Application.Sync;
using Messenger.Domain.Sync;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Sync;

public sealed class UserSyncEventWriter : IUserSyncEventWriter
{
    private readonly MessengerDbContext _dbContext;

    public UserSyncEventWriter(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddForConversationMembersAsync(
        Guid conversationId,
        Guid? messageId,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        // Members who left the conversation stop receiving its events (they contain message bodies).
        var memberUserIds = await GetActiveMemberUserIdsAsync([conversationId], cancellationToken);

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);

        // One query for all members, locked until the caller's transaction commits, so concurrent
        // writers never hand out the same event id to a user.
        var syncCounters = (await _dbContext.LockUserSyncCountersAsync(memberUserIds, cancellationToken))
            .ToDictionary(x => x.UserId);

        foreach (var memberUserId in memberUserIds)
        {
            // A counter created earlier in this unit of work is not in the database yet.
            var syncCounter = syncCounters.GetValueOrDefault(memberUserId)
                ?? _dbContext.UserSyncCounters.Local.FirstOrDefault(x => x.UserId == memberUserId);

            if (syncCounter is null)
            {
                syncCounter = new UserSyncCounter(memberUserId);
                _dbContext.UserSyncCounters.Add(syncCounter);
            }

            var eventId = syncCounter.GetNextEventId();

            _dbContext.UserSyncEvents.Add(
                new UserSyncEvent(
                    eventId,
                    memberUserId,
                    eventType,
                    payloadJson,
                    conversationId,
                    messageId));
        }
    }

    public async Task LockMemberCountersAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var memberUserIds = await GetActiveMemberUserIdsAsync(conversationIds, cancellationToken);

        await _dbContext.LockUserSyncCountersAsync(memberUserIds, cancellationToken);
    }

    private Task<List<Guid>> GetActiveMemberUserIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        return _dbContext.ConversationMembers
            .Where(x =>
                conversationIds.Contains(x.ConversationId) &&
                x.LeftAtUtc == null)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
