using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.BackgroundServices;

public sealed class RefreshTokenCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupWorker> _logger;

    public RefreshTokenCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshTokenCleanupWorker> logger)
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
                _logger.LogError(ex, "Refresh token cleanup failed. It will retry later.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-7);

        var deletedCount = await dbContext.RefreshTokens
            .Where(x =>
                x.ExpiresAtUtc < cutoff ||
                (x.RevokedAtUtc != null && x.RevokedAtUtc < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Deleted {DeletedCount} old refresh tokens.",
                deletedCount);
        }
    }
}