using MassTransit;
using Microsoft.Extensions.Logging;
using Messenger.Contracts.IntegrationEvents;

namespace Messenger.Infrastructure.Consumers;

public sealed class TestIntegrationEventConsumer : IConsumer<TestIntegrationEvent>
{
    private readonly ILogger<TestIntegrationEventConsumer> _logger;

    public TestIntegrationEventConsumer(
        ILogger<TestIntegrationEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<TestIntegrationEvent> context)
    {
        _logger.LogInformation(
            "Consumed TestIntegrationEvent. Id: {EventId}, Message: {Message}",
            context.Message.Id,
            context.Message.Message);

        return Task.CompletedTask;
    }
}