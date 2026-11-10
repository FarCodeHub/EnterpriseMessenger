using Messenger.Application.Auditing;
using Messenger.Application.Messages.DeleteMessage;
using Messenger.Application.Search;
using Messenger.Application.Sync;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageDeleter : IMessageDeleter
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IMessageSearchIndexer _messageSearchIndexer;
    private readonly ILogger<MessageDeleter> _logger;

    public MessageDeleter(MessengerDbContext dbContext, IUserSyncEventWriter syncEventWriter, IAuditLogWriter auditLogWriter, IMessageSearchIndexer messageSearchIndexer, ILogger<MessageDeleter> logger)
    {
        _logger = logger;
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
        _auditLogWriter = auditLogWriter;
        _messageSearchIndexer = messageSearchIndexer;
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

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);


        // The message is already saved; a search outage must not fail the request (the index can be rebuilt).
        try
        {
            await _messageSearchIndexer.DeleteAsync(
                message.Id,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Updating the search index failed for message {MessageId}.", message.Id);
        }

        return new DeleteMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.DeletedAtUtc);
    }


}