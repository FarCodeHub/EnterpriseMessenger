using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Consumers;

public sealed class MessageCreatedIntegrationEventConsumer
    : IConsumer<MessageCreatedIntegrationEvent>
{
    private readonly ILogger<MessageCreatedIntegrationEventConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly MessengerDbContext _dbContext;
    public MessageCreatedIntegrationEventConsumer(
        ILogger<MessageCreatedIntegrationEventConsumer> logger,
        IPublishEndpoint publishEndpoint,
        MessengerDbContext dbContext)
    {
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<MessageCreatedIntegrationEvent> context)
    {
        _logger.LogInformation(
            "MessageCreatedIntegrationEvent consumed. MessageId: {MessageId}, ConversationId: {ConversationId}, SequenceNumber: {SequenceNumber}",
            context.Message.MessageId,
            context.Message.ConversationId,
            context.Message.SequenceNumber);

        var recipientUserIds = await _dbContext.ConversationMembers
            .Where(x =>
                x.ConversationId == context.Message.ConversationId &&
                x.UserId != context.Message.SenderUserId &&
                !x.IsMuted &&
                !x.IsArchived)
            .Select(x => x.UserId)
            .ToListAsync(context.CancellationToken);
            
        await _publishEndpoint.Publish(
           new NotificationRequestedIntegrationEvent(
               Guid.NewGuid(),
               context.Message.MessageId,
               context.Message.ConversationId,
               context.Message.SequenceNumber,
               context.Message.SenderUserId,
               recipientUserIds,
               DateTime.UtcNow),
           context.CancellationToken);
    }
}