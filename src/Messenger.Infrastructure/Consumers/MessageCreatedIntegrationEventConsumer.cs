using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace Messenger.Infrastructure.Consumers;

public sealed class MessageCreatedIntegrationEventConsumer 
    : IConsumer<MessageCreatedIntegrationEvent>
{
    private readonly ILogger<MessageCreatedIntegrationEventConsumer> _logger;

    public MessageCreatedIntegrationEventConsumer(
        ILogger<MessageCreatedIntegrationEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<MessageCreatedIntegrationEvent> context)
    {
        _logger.LogInformation(
            "MessageCreatedIntegrationEvent consumed. MessageId: {MessageId}, ConversationId: {ConversationId}, SequenceNumber: {SequenceNumber}",
            context.Message.MessageId,
            context.Message.ConversationId,
            context.Message.SequenceNumber);

        return Task.CompletedTask;
    }
}