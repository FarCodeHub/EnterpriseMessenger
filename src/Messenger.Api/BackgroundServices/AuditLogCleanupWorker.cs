using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.BackgroundServices;

public sealed class AuditLogCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupWorker> _logger;

    public AuditLogCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogCleanupWorker> logger)
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
                _logger.LogError(ex, "Audit log cleanup failed. It will retry later.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-180);

        var deletedCount = await dbContext.AuditLogs
            .Where(x => x.CreatedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Deleted {DeletedCount} old audit logs.",
                deletedCount);
        }
    }
}