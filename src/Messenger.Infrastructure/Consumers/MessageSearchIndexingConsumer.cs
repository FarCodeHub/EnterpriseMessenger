using MassTransit;
using Messenger.Application.Search;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Consumers;

/// <summary>
/// Adds new messages to the search index from the outbox event, so sending a message never waits for
/// (or fails because of) the search cluster. Failed deliveries are retried by MassTransit.
/// </summary>
public sealed class MessageSearchIndexingConsumer : IConsumer<MessageCreatedIntegrationEvent>
{
    private readonly MessengerDbContext _dbContext;
    private readonly IMessageSearchIndexer _messageSearchIndexer;

    public MessageSearchIndexingConsumer(
        MessengerDbContext dbContext,
        IMessageSearchIndexer messageSearchIndexer)
    {
        _dbContext = dbContext;
        _messageSearchIndexer = messageSearchIndexer;
    }

    public async Task Consume(ConsumeContext<MessageCreatedIntegrationEvent> context)
    {
        // Index the message as it is now: it may have been edited or deleted since the event was written.
        var message = await _dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == context.Message.MessageId, context.CancellationToken);

        if (message is null || message.DeletedAtUtc is not null)
            return;

        await _messageSearchIndexer.IndexAsync(
            new MessageSearchDocument(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.Body,
                message.SequenceNumber,
                message.CreatedAtUtc,
                message.EditedAtUtc,
                IsDeleted: false),
            context.CancellationToken);
    }
}
