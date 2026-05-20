using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace Messenger.Infrastructure.Consumers;

public sealed class MessageCreatedIntegrationEventConsumer
    : IConsumer<MessageCreatedIntegrationEvent>
{
    private readonly ILogger<MessageCreatedIntegrationEventConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public MessageCreatedIntegrationEventConsumer(
        ILogger<MessageCreatedIntegrationEventConsumer> logger,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<MessageCreatedIntegrationEvent> context)
    {
        _logger.LogInformation(
            "MessageCreatedIntegrationEvent consumed. MessageId: {MessageId}, ConversationId: {ConversationId}, SequenceNumber: {SequenceNumber}",
            context.Message.MessageId,
            context.Message.ConversationId,
            context.Message.SequenceNumber);

        await _publishEndpoint.Publish(
            new NotificationRequestedIntegrationEvent(
                Guid.NewGuid(),
                context.Message.MessageId,
                context.Message.ConversationId,
                context.Message.SequenceNumber,
                context.Message.SenderUserId,
                DateTime.UtcNow),
            context.CancellationToken);
    }
}