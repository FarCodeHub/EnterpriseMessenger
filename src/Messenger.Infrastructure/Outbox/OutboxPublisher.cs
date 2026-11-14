using System.Text.Json;
using MassTransit;
using Messenger.Application.Outbox;
using Messenger.Domain.Outbox;
using Messenger.Infrastructure.Observability;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Messenger.Infrastructure.Outbox;

public sealed class OutboxPublisher : IOutboxPublisher
{
    public const int MaxRetries = 5;

    private readonly MessengerDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly int _concurrency;

    public OutboxPublisher(
        MessengerDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IConfiguration configuration,
        ILogger<OutboxPublisher> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _concurrency = configuration.GetValue("Outbox:PublishConcurrency", 32);
    }

    public async Task<int> PublishBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        // The claimed rows stay locked until commit, so another instance skips them instead of publishing them twice.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await ClaimAsync(batchSize, cancellationToken);

        if (messages.Count == 0)
            return 0;

        // Publishing is the slow part (a broker round trip each); the entities are only changed, the context is
        // not used concurrently.
        await Parallel.ForEachAsync(
            messages,
            new ParallelOptions { MaxDegreeOfParallelism = _concurrency, CancellationToken = cancellationToken },
            PublishAsync);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    private Task<List<OutboxMessage>> ClaimAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            return _dbContext.OutboxMessages
                .Where(x => x.ProcessedAtUtc == null && x.RetryCount < MaxRetries)
                .OrderBy(x => x.OccurredAtUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        return _dbContext.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAtUtc" IS NULL AND "RetryCount" < {MaxRetries}
                ORDER BY "OccurredAtUtc"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);
    }

    private async ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var messageType = Type.GetType(message.Type)
                ?? throw new InvalidOperationException($"Cannot resolve outbox message type: {message.Type}");

            var integrationEvent = JsonSerializer.Deserialize(message.Payload, messageType)
                ?? throw new InvalidOperationException("Cannot deserialize outbox message payload.");

            await _publishEndpoint.Publish(integrationEvent, messageType, cancellationToken);

            message.MarkAsProcessed();

            MessengerMetrics.OutboxPublishDelay.Record(
                (DateTime.UtcNow - message.OccurredAtUtc).TotalSeconds,
                new KeyValuePair<string, object?>("type", messageType.Name));

            _logger.LogDebug(
                "Outbox message published. OutboxMessageId: {OutboxMessageId}, Type: {Type}",
                message.Id,
                message.Type);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.MarkAsFailed(exception.Message);

            MessengerMetrics.OutboxPublishFailures.Add(
                1,
                new KeyValuePair<string, object?>("type", ShortTypeName(message.Type)));

            _logger.LogError(exception, "Failed to publish outbox message {OutboxMessageId}", message.Id);
        }
    }

    // "Namespace.TypeName, Assembly, ..." -> "TypeName"
    private static string ShortTypeName(string assemblyQualifiedName) =>
        assemblyQualifiedName.Split(',')[0].Split('.')[^1];
}
