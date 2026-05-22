using Messenger.Domain.Outbox;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.BackgroundServices;

public sealed class DeadLetterOutboxMoverWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadLetterOutboxMoverWorker> _logger;

    public DeadLetterOutboxMoverWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DeadLetterOutboxMoverWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await MoveFailedMessagesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task MoveFailedMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var failedMessages = await dbContext.OutboxMessages
            .Where(x =>
                x.ProcessedAtUtc == null &&
                x.RetryCount >= 5)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var message in failedMessages)
        {
            dbContext.DeadLetterOutboxMessages.Add(
                new DeadLetterOutboxMessage(
                    message.Id,
                    message.Type,
                    message.Payload,
                    message.Error ?? "Unknown error",
                    message.RetryCount));

            dbContext.OutboxMessages.Remove(message);
        }

        if (failedMessages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Moved {Count} outbox messages to dead-letter.",
                failedMessages.Count);
        }
    }
}