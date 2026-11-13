using Messenger.Application.Auditing;
using Messenger.Application.Messages.EditMessage;
using Messenger.Application.Sync;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Messenger.Application.Search;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageEditor : IMessageEditor
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IMessageSearchIndexer _messageSearchIndexer;
    private readonly ILogger<MessageEditor> _logger;

    public MessageEditor(MessengerDbContext dbContext, IUserSyncEventWriter syncEventWriter, IAuditLogWriter auditLogWriter, IMessageSearchIndexer messageSearchIndexer, ILogger<MessageEditor> logger)
    {
        _logger = logger;
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
        _auditLogWriter = auditLogWriter;
        _messageSearchIndexer = messageSearchIndexer;
    }

    public async Task<EditMessageResult> EditAsync(
        EditMessageCommand command,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == command.MessageId, cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.SenderUserId != command.UserId)
            throw new UnauthorizedAccessException("You can only edit your own messages.");

        // The sync event writer locks members' counters until commit.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        message.Edit(command.Body);

        await _auditLogWriter.WriteAsync(
    "MessageEdited",
    command.UserId,
    "Message",
    message.Id.ToString(),
    new
    {
        message.ConversationId,
        message.SequenceNumber,
        message.EditedAtUtc
    },
    cancellationToken);

   await _syncEventWriter.AddForConversationMembersAsync(
    message.ConversationId,
    message.Id,
    "MessageEdited",
    new
    {
        MessageId = message.Id,
        message.ConversationId,
        message.SequenceNumber,
        message.Body,
        message.EditedAtUtc
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);


        // The message is already saved; a search outage must not fail the request (the index can be rebuilt).
        try
        {
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Updating the search index failed for message {MessageId}.", message.Id);
        }
var sender = await _dbContext.Users
    .Where(x => x.Id == message.SenderUserId)
    .Select(x => new
    {
        x.DisplayName,
        x.UserName
    })
    .FirstOrDefaultAsync(cancellationToken);

var senderUserDisplayName =
    sender?.DisplayName ?? sender?.UserName;

        return new EditMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.SenderUserId,
            senderUserDisplayName,
            message.Body,
            message.EditedAtUtc);
    }
 
}