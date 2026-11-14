using Messenger.Infrastructure.Observability;
using System.Text.Json;
using MassTransit;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.BackgroundServices;

public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await PublishPendingMessagesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Outbox publisher worker failed. It will retry later.");
        }

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }
}

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await dbContext.OutboxMessages
            .Where(x => x.ProcessedAtUtc == null && x.RetryCount < 5)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var messageType = Type.GetType(message.Type);

                if (messageType is null)
                    throw new InvalidOperationException($"Cannot resolve outbox message type: {message.Type}");

                var integrationEvent = JsonSerializer.Deserialize(
                    message.Payload,
                    messageType);

                if (integrationEvent is null)
                    throw new InvalidOperationException("Cannot deserialize outbox message payload.");

                await publishEndpoint.Publish(
                    integrationEvent,
                    messageType,
                    cancellationToken);

                message.MarkAsProcessed();

                MessengerMetrics.OutboxPublishDelay.Record(
                    (DateTime.UtcNow - message.OccurredAtUtc).TotalSeconds,
                    new KeyValuePair<string, object?>("type", messageType.Name));

                _logger.LogInformation(
                    "Outbox message published. OutboxMessageId: {OutboxMessageId}, Type: {Type}",
                    message.Id,
                    message.Type);
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);

                MessengerMetrics.OutboxPublishFailures.Add(
                    1,
                    new KeyValuePair<string, object?>("type", ShortTypeName(message.Type)));

                _logger.LogError(
                    ex,
                    "Failed to publish outbox message {OutboxMessageId}",
                    message.Id);
            }
        }

        if (messages.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
    }

    // "Namespace.TypeName, Assembly, ..." -> "TypeName"
    private static string ShortTypeName(string assemblyQualifiedName) =>
        assemblyQualifiedName.Split(',')[0].Split('.')[^1];
}
