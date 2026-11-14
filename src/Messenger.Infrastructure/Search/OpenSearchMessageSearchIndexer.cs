using Messenger.Application.Search;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;

namespace Messenger.Infrastructure.Search;

public sealed class OpenSearchMessageSearchIndexer : IMessageSearchIndexer
{
    private readonly IOpenSearchClient _client;
    private readonly string _indexName;
    private readonly MessengerDbContext _dbContext;

public OpenSearchMessageSearchIndexer(
    IOpenSearchClient client,
    IConfiguration configuration,
    MessengerDbContext dbContext)
{
    _client = client;
    _dbContext = dbContext;
    _indexName = configuration["OpenSearch:MessagesIndex"]
        ?? "messenger-messages";
}

    public async Task IndexAsync(
        MessageSearchDocument document,
        CancellationToken cancellationToken)
    {
        var response = await _client.IndexAsync(
            document,
            x => x
                .Index(_indexName)
                .Id(document.Id),
            cancellationToken);

        if (!response.IsValid)
            throw new InvalidOperationException(
                response.ServerError?.ToString() ?? response.OriginalException?.Message);
    }

    public async Task DeleteAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var response = await _client.DeleteAsync<MessageSearchDocument>(
            messageId,
            x => x.Index(_indexName),
            cancellationToken);

        if (!response.IsValid && response.Result != Result.NotFound)
            throw new InvalidOperationException(
                response.ServerError?.ToString() ?? response.OriginalException?.Message);
    }


public async Task EnsureIndexAsync(CancellationToken cancellationToken)
{
    var existsResponse = await _client.Indices.ExistsAsync(
        _indexName,
        ct: cancellationToken);

    if (existsResponse.Exists)
        return;

var createResponse = await _client.Indices.CreateAsync(
    _indexName,
    c => c
        .Settings(s => s
            .Analysis(a => a
                .CharFilters(cf => cf
                    .Mapping("persian_char_filter", m => m
                        .Mappings(
                            "ي => ی",
                            "ك => ک",
                            "ۀ => ه",
                            "ة => ه"
                        )
                    )
                )
                .Analyzers(an => an
                    .Custom("persian_messenger_analyzer", ca => ca
                        .CharFilters("persian_char_filter")
                        .Tokenizer("standard")
                        .Filters("lowercase", "arabic_normalization", "persian_normalization")
                    )
                )
            )
        )
        .Map<MessageSearchDocument>(m => m
            .Properties(p => p
                .Keyword(k => k.Name(x => x.Id))
                .Keyword(k => k.Name(x => x.ConversationId))
                .Keyword(k => k.Name(x => x.SenderUserId))
                .Text(t => t
                    .Name(x => x.Body)
                    .Analyzer("persian_messenger_analyzer")
                    .SearchAnalyzer("persian_messenger_analyzer"))
                .Number(n => n.Name(x => x.SequenceNumber).Type(NumberType.Long))
                .Date(d => d.Name(x => x.CreatedAtUtc))
                .Date(d => d.Name(x => x.EditedAtUtc))
                .Boolean(b => b.Name(x => x.IsDeleted))
            )),
    cancellationToken);

if (!createResponse.IsValid)
{
    throw new InvalidOperationException(
        $"""
        OpenSearch create index failed.
        Index: {_indexName}
        DebugInformation: {createResponse.DebugInformation}
        ServerError: {createResponse.ServerError}
        OriginalException: {createResponse.OriginalException?.Message}
        """,
        createResponse.OriginalException);
}
}




public const int RebuildBatchSize = 1_000;

    public async Task<long> RebuildAsync(Action<long>? onProgress, CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken);

        long indexed = 0;
        var lastId = Guid.Empty;

        while (true)
        {
            // Keyset paging by id: every page is an index range scan, and only one page is in memory.
            var page = await NextPageAsync(lastId, cancellationToken);

            if (page.Count == 0)
                break;

            var response = await _client.BulkAsync(
                bulk => bulk.Index(_indexName).IndexMany(page, (operation, document) => operation.Id(document.Id)),
                cancellationToken);

            if (!response.IsValid || response.Errors)
            {
                var firstError = response.ItemsWithErrors.FirstOrDefault()?.Error?.ToString();
                throw new InvalidOperationException(
                    firstError ?? response.ServerError?.ToString() ?? response.OriginalException?.Message);
            }

            indexed += page.Count;
            lastId = page[^1].Id;
            onProgress?.Invoke(indexed);
        }

        return indexed;
    }

    private async Task<List<MessageSearchDocument>> NextPageAsync(Guid afterId, CancellationToken cancellationToken)
    {
        var messages = _dbContext.Database.IsNpgsql()
            ? _dbContext.Messages.FromSql($"""
                SELECT * FROM "Messages"
                WHERE "DeletedAtUtc" IS NULL AND "Id" > {afterId}
                ORDER BY "Id"
                LIMIT {RebuildBatchSize}
                """)
            : _dbContext.Messages
                .Where(x => x.DeletedAtUtc == null && x.Id.CompareTo(afterId) > 0)
                .OrderBy(x => x.Id)
                .Take(RebuildBatchSize);

        // Ordered again outside: the SQL above becomes a subquery, whose order PostgreSQL need not keep. The order
        // must be the database's uuid order (not .NET's Guid order) so the last id is the next page's start.
        return await messages
            .OrderBy(x => x.Id)
            .AsNoTracking()
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
    }
}
