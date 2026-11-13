using Messenger.Application.Auditing;
using Messenger.Application.Messages.DeleteMessage;
using Messenger.Application.Outbox;
using Messenger.Application.Sync;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageDeleter : IMessageDeleter
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IOutboxWriter _outboxWriter;

    public MessageDeleter(
        MessengerDbContext dbContext,
        IUserSyncEventWriter syncEventWriter,
        IAuditLogWriter auditLogWriter,
        IOutboxWriter outboxWriter)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
        _auditLogWriter = auditLogWriter;
        _outboxWriter = outboxWriter;
    }

    public async Task<DeleteMessageResult> DeleteAsync(
        DeleteMessageCommand command,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == command.MessageId, cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.SenderUserId != command.UserId)
            throw new UnauthorizedAccessException("You can only delete your own messages.");

        // The sync event writer locks members' counters until commit.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        message.Delete();

        await _auditLogWriter.WriteAsync(
    "MessageDeleted",
    command.UserId,
    "Message",
    message.Id.ToString(),
    new
    {
        message.ConversationId,
        message.SequenceNumber,
        message.DeletedAtUtc
    },
    cancellationToken);

        await _syncEventWriter.AddForConversationMembersAsync(
          message.ConversationId,
          message.Id,
          "MessageDeleted",
          new
          {
              MessageId = message.Id,
              message.ConversationId,
              message.SequenceNumber,
              message.DeletedAtUtc
          },
          cancellationToken);

        // The search index (and later integrations) are updated from this event, outside the request.
        await _outboxWriter.AddAsync(
            new MessageDeletedIntegrationEvent(
                message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.DeletedAtUtc!.Value),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DeleteMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.DeletedAtUtc);
    }

}