using Messenger.Domain.Conversations;
using Messenger.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Persistence;

/// <summary>
/// Loads counter rows with <c>SELECT ... FOR UPDATE</c> so concurrent writers get unique, gap-free numbers.
/// Call inside a transaction: the row locks are held until it commits. Rows are always locked in id order
/// to avoid deadlocks between transactions that lock several counters.
/// </summary>
internal static class RowLockExtensions
{
    public static async Task<List<ConversationCounter>> LockConversationCountersAsync(
        this MessengerDbContext dbContext,
        IEnumerable<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var ids = conversationIds.Distinct().ToArray();

        if (ids.Length == 0)
            return [];

        if (!dbContext.Database.IsNpgsql())
        {
            return await dbContext.ConversationCounters
                .Where(x => ids.Contains(x.ConversationId))
                .ToListAsync(cancellationToken);
        }

        return await dbContext.ConversationCounters
            .FromSql($"""
                SELECT * FROM "ConversationCounters"
                WHERE "ConversationId" = ANY({ids})
                ORDER BY "ConversationId"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
    }

    public static async Task<List<UserSyncCounter>> LockUserSyncCountersAsync(
        this MessengerDbContext dbContext,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();

        if (ids.Length == 0)
            return [];

        if (!dbContext.Database.IsNpgsql())
        {
            return await dbContext.UserSyncCounters
                .Where(x => ids.Contains(x.UserId))
                .ToListAsync(cancellationToken);
        }

        return await dbContext.UserSyncCounters
            .FromSql($"""
                SELECT * FROM "UserSyncCounters"
                WHERE "UserId" = ANY({ids})
                ORDER BY "UserId"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
    }

    /// <summary>True when saving failed because the unique (ConversationId, ClientMessageId) index was hit.</summary>
    public static bool IsClientMessageIdConflict(this DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } postgres &&
        postgres.ConstraintName?.Contains("ClientMessageId", StringComparison.Ordinal) == true;
}
