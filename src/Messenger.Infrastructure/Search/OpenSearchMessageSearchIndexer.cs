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
            .Map<MessageSearchDocument>(m => m
                .Properties(p => p
                    .Keyword(k => k.Name(x => x.Id))
                    .Keyword(k => k.Name(x => x.ConversationId))
                    .Keyword(k => k.Name(x => x.SenderUserId))
                    .Text(t => t.Name(x => x.Body))
                    .Number(n => n.Name(x => x.SequenceNumber).Type(NumberType.Long))
                    .Date(d => d.Name(x => x.CreatedAtUtc))
                    .Date(d => d.Name(x => x.EditedAtUtc))
                    .Boolean(b => b.Name(x => x.IsDeleted))
                )),
        cancellationToken);

    if (!createResponse.IsValid)
        throw new InvalidOperationException(
            createResponse.ServerError?.ToString()
            ?? createResponse.OriginalException?.Message);
}




public async Task RebuildAsync(CancellationToken cancellationToken)
{
    await EnsureIndexAsync(cancellationToken);

    var messages = await _dbContext.Messages
        .Where(x => x.DeletedAtUtc == null)
        .Select(x => new MessageSearchDocument(
            x.Id,
            x.ConversationId,
            x.SenderUserId,
            x.Body,
            x.SequenceNumber,
            x.CreatedAtUtc,
            x.EditedAtUtc,
            IsDeleted: false))
        .ToListAsync(cancellationToken);

    foreach (var message in messages)
    {
        await IndexAsync(message, cancellationToken);
    }
}



}