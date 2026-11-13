using Messenger.Application.Search;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Search;

/// <summary>
/// Fallback message search used when OpenSearch is not configured. A plain substring match over the
/// messages of conversations the user is an active member of. It cannot use an index, so it is meant for
/// development and small installations; production should run OpenSearch.
/// </summary>
public sealed class PostgresMessageFullTextSearchService : IMessageFullTextSearchService
{
    private readonly MessengerDbContext _dbContext;

    public PostgresMessageFullTextSearchService(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MessageSearchResult> SearchAsync(
        MessageSearchQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var text = NormalizePersian(query.Text.Trim()).ToLower();

        var memberConversationIds = _dbContext.ConversationMembers
            .Where(x => x.UserId == query.CurrentUserId && x.LeftAtUtc == null)
            .Select(x => x.ConversationId);

        var messages = _dbContext.Messages
            .AsNoTracking()
            .Where(x =>
                x.DeletedAtUtc == null &&
                memberConversationIds.Contains(x.ConversationId) &&
                x.Body.Replace("ي", "ی").Replace("ك", "ک").ToLower().Contains(text));

        if (query.ConversationId.HasValue)
        {
            var conversationId = query.ConversationId.Value;
            messages = messages.Where(x => x.ConversationId == conversationId);
        }

        var totalCount = await messages.LongCountAsync(cancellationToken);

        var documents = await messages
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MessageSearchDocument(
                x.Id,
                x.ConversationId,
                x.SenderUserId,
                x.Body,
                x.SequenceNumber,
                x.CreatedAtUtc,
                x.EditedAtUtc,
                false))
            .ToListAsync(cancellationToken);

        return new MessageSearchResult(
            page,
            pageSize,
            totalCount,
            documents.Select(x => new MessageSearchResultItem(x, null)).ToList());
    }

    // Arabic Yeh/Kaf to their Persian forms, matching the OpenSearch char filter.
    private static string NormalizePersian(string value)
        => value.Replace('ي', 'ی').Replace('ك', 'ک');
}
