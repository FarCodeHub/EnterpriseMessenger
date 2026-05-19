using Messenger.Application.Messages.SearchMessages;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class PostgresMessageSearchService : IMessageSearchService
{
    private readonly MessengerDbContext _dbContext;

    public PostgresMessageSearchService(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SearchMessagesResult> SearchAsync(
        SearchMessagesQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
            throw new InvalidOperationException("Search query is required.");

        var take = Math.Clamp(query.Take, 1, 50);

        var messages = await _dbContext.Messages
            .Where(x =>
                x.ConversationId == query.ConversationId &&
                EF.Functions.ILike(x.Body, $"%{query.Query}%"))
            .OrderByDescending(x => x.SequenceNumber)
            .Take(take)
            .Select(x => new SearchMessageResult(
                x.Id,
                x.ConversationId,
                x.SequenceNumber,
                x.SenderUserId,
                x.Body,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new SearchMessagesResult(
            query.ConversationId,
            query.Query,
            messages.Count,
            messages);
    }
}