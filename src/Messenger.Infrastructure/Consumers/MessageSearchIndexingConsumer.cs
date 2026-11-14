using MassTransit;
using Messenger.Application.Search;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Consumers;

/// <summary>
/// Keeps the search index in step with messages, from outbox events, so sending, editing and deleting never wait
/// for (or fail because of) the search cluster. Failed deliveries are retried by MassTransit.
/// <para>
/// Every event re-reads the message and indexes its current state: events may arrive late or out of order
/// (an edit before its create), and handling one twice is harmless.
/// </para>
/// </summary>
public sealed class MessageSearchIndexingConsumer :
    IConsumer<MessageCreatedIntegrationEvent>,
    IConsumer<MessageEditedIntegrationEvent>,
    IConsumer<MessageDeletedIntegrationEvent>
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

    public Task Consume(ConsumeContext<MessageCreatedIntegrationEvent> context) =>
        SyncAsync(context.Message.MessageId, context.CancellationToken);

    public Task Consume(ConsumeContext<MessageEditedIntegrationEvent> context) =>
        SyncAsync(context.Message.MessageId, context.CancellationToken);

    public Task Consume(ConsumeContext<MessageDeletedIntegrationEvent> context) =>
        SyncAsync(context.Message.MessageId, context.CancellationToken);

    private async Task SyncAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message is null || message.DeletedAtUtc is not null)
        {
            await _messageSearchIndexer.DeleteAsync(messageId, cancellationToken);
            return;
        }

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
            cancellationToken);
    }
}
