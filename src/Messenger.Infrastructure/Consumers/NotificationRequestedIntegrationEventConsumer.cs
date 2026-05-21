using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Messenger.Application.Realtime;
namespace Messenger.Infrastructure.Consumers;

public sealed class NotificationRequestedIntegrationEventConsumer
    : IConsumer<NotificationRequestedIntegrationEvent>
{
    private readonly ILogger<NotificationRequestedIntegrationEventConsumer> _logger;
    private readonly IConnectionTracker _connectionTracker;
    public NotificationRequestedIntegrationEventConsumer(
        ILogger<NotificationRequestedIntegrationEventConsumer> logger, IConnectionTracker connectionTracker)
    {
        _logger = logger;
        _connectionTracker = connectionTracker;
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

        _logger.LogInformation(
            "Push notification should be sent to offline user {UserId} for message {MessageId}",
            recipientUserId,
            context.Message.MessageId);
    }
}
}