using Messenger.Application.Auditing;
using Messenger.Application.Messages.EditMessage;
using Messenger.Application.Outbox;
using Messenger.Application.Sync;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageEditor : IMessageEditor
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IOutboxWriter _outboxWriter;

    public MessageEditor(
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

        // The search index (and later integrations) are updated from this event, outside the request.
        await _outboxWriter.AddAsync(
            new MessageEditedIntegrationEvent(
                message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.SenderUserId,
                message.Body,
                message.EditedAtUtc!.Value),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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