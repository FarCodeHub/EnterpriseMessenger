using MassTransit;
using Messenger.Application.Realtime;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Domain.Notifications;
using Messenger.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

public sealed class NotificationRequestedIntegrationEventConsumer
    : IConsumer<NotificationRequestedIntegrationEvent>
{
    private readonly ILogger<NotificationRequestedIntegrationEventConsumer> _logger;
    private readonly IConnectionTracker _connectionTracker;
    private readonly MessengerDbContext _dbContext;

    public NotificationRequestedIntegrationEventConsumer(
        ILogger<NotificationRequestedIntegrationEventConsumer> logger,
        IConnectionTracker connectionTracker,
        MessengerDbContext dbContext)
    {
        _logger = logger;
        _connectionTracker = connectionTracker;
        _dbContext = dbContext;
    }

    public async Task Consume(
        ConsumeContext<NotificationRequestedIntegrationEvent> context)
    {
        foreach (var recipientUserId in context.Message.RecipientUserIds)
        {
            var isOnline = await _connectionTracker.IsOnlineAsync(recipientUserId);

            if (isOnline)
            {
                _logger.LogInformation(
                    "Skipping push notification for online user {UserId}",
                    recipientUserId);

                continue;
            }

            var notification = new Notification(
                recipientUserId,
                context.Message.MessageId,
                context.Message.ConversationId,
                context.Message.RequestedAtUtc);

            _dbContext.Notifications.Add(notification);

            _logger.LogInformation(
                "Pending notification created for offline user {UserId}, MessageId: {MessageId}",
                recipientUserId,
                context.Message.MessageId);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}