using Messenger.Application.Search;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;

namespace Messenger.Infrastructure.Search;

public sealed class OpenSearchMessageFullTextSearchService : IMessageFullTextSearchService
{
    private readonly IOpenSearchClient _client;
    private readonly MessengerDbContext _dbContext;
    private readonly string _indexName;

    public OpenSearchMessageFullTextSearchService(
        IOpenSearchClient client,
        MessengerDbContext dbContext,
        IConfiguration configuration)
    {
        _client = client;
        _dbContext = dbContext;
        _indexName = configuration["OpenSearch:MessagesIndex"] ?? "messenger-messages";
    }

    public async Task<MessageSearchResult> SearchAsync(
        MessageSearchQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        var allowedConversationIds = await _dbContext.ConversationMembers
            .Where(x =>
                x.UserId == query.CurrentUserId &&
                x.LeftAtUtc == null)
            .Select(x => x.ConversationId)
            .ToListAsync(cancellationToken);

        if (query.ConversationId.HasValue)
        {
            if (!allowedConversationIds.Contains(query.ConversationId.Value))
            {
                return new MessageSearchResult(
                    page,
                    pageSize,
                    0,
                    Array.Empty<MessageSearchDocument>());
            }

            allowedConversationIds = [query.ConversationId.Value];
        }

        if (allowedConversationIds.Count == 0)
        {
            return new MessageSearchResult(
                page,
                pageSize,
                0,
                Array.Empty<MessageSearchDocument>());
        }

        var response = await _client.SearchAsync<MessageSearchDocument>(
            s => s
                .Index(_indexName)
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.Match(mm => mm
                                .Field(f => f.Body)
                                .Query(query.Text)
                            )
                        )
                        .Filter(
                            f => f.Term(t => t.Field(x => x.IsDeleted).Value(false)),
                            f => f.Terms(t => t
                                .Field(x => x.ConversationId)
                                .Terms(allowedConversationIds.Select(x => x.ToString()))
                            )
                        )
                    )
                )
                .Sort(sort => sort
                    .Descending(x => x.CreatedAtUtc)),
            cancellationToken);

        if (!response.IsValid)
            throw new InvalidOperationException(
                response.ServerError?.ToString()
                ?? response.OriginalException?.Message);

        return new MessageSearchResult(
            page,
            pageSize,
            response.Total,
            response.Documents.ToList());
    }
}