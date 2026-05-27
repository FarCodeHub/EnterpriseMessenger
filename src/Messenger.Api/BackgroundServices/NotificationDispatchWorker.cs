using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.BackgroundServices;

public sealed class NotificationDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatchWorker> _logger;

    public NotificationDispatchWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDispatchWorker> logger)
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
            await ProcessPendingNotificationsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Outbox publisher worker failed. It will retry later.");
        }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessPendingNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var pendingNotifications = await dbContext.Notifications
            .Where(x => x.Status == "Pending")
            .OrderBy(x => x.RequestedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var notification in pendingNotifications)
        {
            try
            {
                _logger.LogInformation(
                    "Fake push notification sent. NotificationId: {NotificationId}, UserId: {UserId}, MessageId: {MessageId}",
                    notification.Id,
                    notification.UserId,
                    notification.MessageId);

                notification.MarkAsSent();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send notification {NotificationId}",
                    notification.Id);

                notification.MarkAsFailed();
            }
        }

        if (pendingNotifications.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
    }
}