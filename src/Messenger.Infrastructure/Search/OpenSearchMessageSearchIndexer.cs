using Messenger.Application.Search;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace Messenger.Infrastructure.Search;

public sealed class OpenSearchMessageSearchIndexer : IMessageSearchIndexer
{
    private readonly IOpenSearchClient _client;
    private readonly ILogger<OpenSearchMessageSearchIndexer> _logger;
    private readonly string _indexName;
    private readonly MessengerDbContext _dbContext;

public OpenSearchMessageSearchIndexer(
    IOpenSearchClient client,
    IConfiguration configuration,
    MessengerDbContext dbContext,
    ILogger<OpenSearchMessageSearchIndexer> logger)
{
    _client = client;
    _dbContext = dbContext;
    _logger = logger;
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
        // _indexName is an alias after the first rebuild, or an index created by an older version.
        var existsResponse = await _client.Indices.ExistsAsync(_indexName, ct: cancellationToken);

        if (!existsResponse.Exists)
        {
            await CreateIndexAsync(_indexName, cancellationToken);
            return;
        }

        // An index created implicitly (by a write before it existed) has dynamic mappings: conversationId is text,
        // so the membership filter never matches and search finds nothing. Only a rebuild can fix the mapping.
        var mapping = await _client.Indices.GetMappingAsync<MessageSearchDocument>(m => m.Index(_indexName), cancellationToken);
        var conversationId = mapping.Indices.Values
            .Select(x => x.Mappings.Properties is { } properties &&
                         properties.TryGetValue("conversationId", out var property) ? property : null)
            .FirstOrDefault();

        if (conversationId is not null && conversationId.Type != "keyword")
        {
            _logger.LogError(
                "Search index {Index} has the wrong mapping (conversationId is {Type}, not keyword): message search " +
                "returns nothing. Rebuild it: POST /api/v1/search/messages/rebuild-index.",
                _indexName,
                conversationId.Type);
        }
    }

    private async Task CreateIndexAsync(string name, CancellationToken cancellationToken)
    {
        var createResponse = await _client.Indices.CreateAsync(
            name,
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
                $"OpenSearch could not create index {name}: {createResponse.ServerError} {createResponse.OriginalException?.Message}",
                createResponse.OriginalException);
        }
    }

    public const int RebuildBatchSize = 1_000;

    /// <summary>
    /// Builds a fresh index (correct mapping and analyzer) next to the live one, then points the
    /// <c>OpenSearch:MessagesIndex</c> alias at it in one atomic step: search keeps working during the rebuild,
    /// and there is never a moment without the name, in which a write would create an index with dynamic mappings.
    /// </summary>
    public async Task<long> RebuildAsync(Action<long>? onProgress, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var newIndex = $"{_indexName}-{startedAt:yyyyMMddHHmmss}";

        await CreateIndexAsync(newIndex, cancellationToken);

        long indexed = 0;
        var lastId = Guid.Empty;

        while (true)
        {
            // Keyset paging by id: every page is an index range scan, and only one page is in memory.
            var page = await NextPageAsync(lastId, cancellationToken);

            if (page.Count == 0)
                break;

            await BulkIndexAsync(newIndex, page, cancellationToken);

            indexed += page.Count;
            lastId = page[^1].Id;
            onProgress?.Invoke(indexed);
        }

        await SwitchAliasAsync(newIndex, cancellationToken);

        // Writes during the rebuild went to the old index (and keyset paging may have passed new ids): apply
        // everything that changed since the rebuild started once more, now through the alias.
        await CatchUpAsync(startedAt, cancellationToken);

        return indexed;
    }

    private async Task BulkIndexAsync(string index, IReadOnlyCollection<MessageSearchDocument> documents, CancellationToken cancellationToken)
    {
        var response = await _client.BulkAsync(
            bulk => bulk.Index(index).IndexMany(documents, (operation, document) => operation.Id(document.Id)),
            cancellationToken);

        if (!response.IsValid || response.Errors)
        {
            var firstError = response.ItemsWithErrors.FirstOrDefault()?.Error?.ToString();
            throw new InvalidOperationException(
                firstError ?? response.ServerError?.ToString() ?? response.OriginalException?.Message);
        }
    }

    private async Task SwitchAliasAsync(string newIndex, CancellationToken cancellationToken)
    {
        var previous = (await _client.GetIndicesPointingToAliasAsync(_indexName)).ToList();
        var isConcreteIndex = previous.Count == 0 &&
            (await _client.Indices.ExistsAsync(_indexName, ct: cancellationToken)).Exists;

        var response = await _client.Indices.BulkAliasAsync(aliases =>
        {
            aliases.Add(add => add.Index(newIndex).Alias(_indexName));

            // An index from an older version has the alias's name: remove it in the same atomic step.
            if (isConcreteIndex)
                aliases.RemoveIndex(remove => remove.Index(_indexName));

            foreach (var index in previous)
                aliases.Remove(remove => remove.Index(index).Alias(_indexName));

            return aliases;
        }, cancellationToken);

        if (!response.IsValid)
            throw new InvalidOperationException(response.ServerError?.ToString() ?? response.OriginalException?.Message);

        foreach (var index in previous)
            await _client.Indices.DeleteAsync(index, ct: cancellationToken);
    }

    private async Task CatchUpAsync(DateTime since, CancellationToken cancellationToken)
    {
        var changed = await _dbContext.Messages
            .AsNoTracking()
            .Where(x => x.CreatedAtUtc >= since || x.EditedAtUtc >= since || x.DeletedAtUtc >= since)
            .ToListAsync(cancellationToken);

        var live = changed
            .Where(x => x.DeletedAtUtc is null)
            .Select(x => new MessageSearchDocument(
                x.Id, x.ConversationId, x.SenderUserId, x.Body, x.SequenceNumber, x.CreatedAtUtc, x.EditedAtUtc, false))
            .ToList();

        if (live.Count > 0)
            await BulkIndexAsync(_indexName, live, cancellationToken);

        foreach (var deleted in changed.Where(x => x.DeletedAtUtc is not null))
            await DeleteAsync(deleted.Id, cancellationToken);
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
