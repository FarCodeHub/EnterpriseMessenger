using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.BackgroundServices;

public sealed class OutboxCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxCleanupWorker> _logger;

    public OutboxCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxCleanupWorker> logger)
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
            await CleanupAsync(stoppingToken);

                    }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Outbox publisher worker failed. It will retry later.");
        }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-7);

        var deletedCount = await dbContext.OutboxMessages
            .Where(x =>
                x.ProcessedAtUtc != null &&
                x.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Deleted {DeletedCount} processed outbox messages.",
                deletedCount);
        }
    }
}