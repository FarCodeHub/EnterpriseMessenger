using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace Messenger.Infrastructure.Consumers;

public sealed class NotificationRequestedIntegrationEventConsumer
    : IConsumer<NotificationRequestedIntegrationEvent>
{
    private readonly ILogger<NotificationRequestedIntegrationEventConsumer> _logger;

    public NotificationRequestedIntegrationEventConsumer(
        ILogger<NotificationRequestedIntegrationEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<NotificationRequestedIntegrationEvent> context)
    {
        _logger.LogInformation(
            "Notification requested. NotificationId: {NotificationId}, MessageId: {MessageId}, ConversationId: {ConversationId}",
            context.Message.NotificationId,
            context.Message.MessageId,
            context.Message.ConversationId);

        return Task.CompletedTask;
    }
}