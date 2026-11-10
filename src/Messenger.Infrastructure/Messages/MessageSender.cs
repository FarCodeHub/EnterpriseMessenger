using Messenger.Application.Messages.SendMessage;
using Messenger.Application.Sync;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Application.Auditing;
using Messenger.Application.Outbox;
using Messenger.Application.Conversations;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageSender : IMessageSender
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IConversationPermissionChecker _permissionChecker;

    public MessageSender(
        MessengerDbContext dbContext,
        IUserSyncEventWriter syncEventWriter,
        IAuditLogWriter auditLogWriter,
        IOutboxWriter outboxWriter,
        IConversationPermissionChecker permissionChecker)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
        _auditLogWriter = auditLogWriter;
        _outboxWriter = outboxWriter;
        _permissionChecker = permissionChecker;
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        var hasBody = !string.IsNullOrWhiteSpace(command.Body);
        var hasAttachments = command.Attachments?.Any() == true;

        if (!hasBody && !hasAttachments)
        {
            throw new InvalidOperationException(
                "Message must contain text or attachment.");
        }

        await _permissionChecker.EnsureActiveMemberAsync(
            command.ConversationId,
            command.SenderUserId,
            cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Locks the conversation's counter until commit: messages in one conversation are numbered one at a
        // time, so sequence numbers are unique and have no gaps even under concurrent sends.
        var counter = (await _dbContext.LockConversationCountersAsync(
                [command.ConversationId],
                cancellationToken))
            .SingleOrDefault();

        if (counter is null)
            throw new InvalidOperationException("Conversation counter not found.");

        // Checked after taking the lock, so a retried request that raced the original sees its message.
        var duplicateMessage = await FindByClientMessageIdAsync(command, cancellationToken);

        if (duplicateMessage is not null)
            return ToDuplicateResult(duplicateMessage);

        long? replyToSequenceNumber = null;

        if (command.ReplyToMessageId is not null)
        {
            var replyToMessage = await _dbContext.Messages
                .Where(x =>
                    x.Id == command.ReplyToMessageId &&
                    x.ConversationId == command.ConversationId)
                .Select(x => new
                {
                    x.Id,
                    x.SequenceNumber
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (replyToMessage is null)
                throw new InvalidOperationException("Reply target message not found.");

            replyToSequenceNumber = replyToMessage.SequenceNumber;
        }

        var nextSequenceNumber = counter.GetNextSequenceNumber();

        var message = new Message(
            command.ConversationId,
            nextSequenceNumber,
            command.SenderUserId,
            command.ClientMessageId,
            command.Body,
            command.ReplyToMessageId,
            replyToSequenceNumber);

        _dbContext.Messages.Add(message);

        var createdAttachments = new List<Messenger.Domain.Attachments.Attachment>();

        if (command.Attachments is not null && command.Attachments.Count > 0)
        {
            var fileIds = command.Attachments
                .Select(x => x.FileId)
                .Distinct()
                .ToList();

            // Only files uploaded by the sender can be attached; metadata comes from the upload record.
            var uploadedFiles = await _dbContext.UploadedFiles
                .Where(x =>
                    fileIds.Contains(x.Id) &&
                    x.UploaderUserId == command.SenderUserId)
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var attachment in command.Attachments)
            {
                if (!uploadedFiles.TryGetValue(attachment.FileId, out var uploadedFile))
                    throw new InvalidOperationException("Attachment file not found.");

                var createdAttachment = new Messenger.Domain.Attachments.Attachment(
                    message.Id,
                    uploadedFile.FileName,
                    uploadedFile.ContentType,
                    uploadedFile.Size,
                    uploadedFile.StoragePath);

                createdAttachments.Add(createdAttachment);
                _dbContext.Attachments.Add(createdAttachment);
            }
        }

        var conversation = await _dbContext.Conversations
            .FirstAsync(x => x.Id == command.ConversationId, cancellationToken);

        conversation.UpdateLastMessage(message.Id, message.CreatedAtUtc);

        // Members who left the conversation no longer receive its messages.
        var memberUserIds = await _dbContext.ConversationMembers
            .Where(x =>
                x.ConversationId == command.ConversationId &&
                x.LeftAtUtc == null)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        foreach (var memberUserId in memberUserIds)
        {
            _dbContext.MessageDeliveries.Add(
                new MessageDelivery(message.Id, memberUserId));
        }

        await _syncEventWriter.AddForConversationMembersAsync(
            message.ConversationId,
            message.Id,
            "MessageCreated",
            new
            {
                MessageId = message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.SenderUserId,
                message.Body,
                message.ReplyToMessageId,
                message.ReplyToSequenceNumber,
                message.CreatedAtUtc
            },
            cancellationToken);

        await _auditLogWriter.WriteAsync(
            "MessageSent",
            message.SenderUserId,
            "Message",
            message.Id.ToString(),
            new
            {
                message.ConversationId,
                message.SequenceNumber,
                message.ClientMessageId
            },
            cancellationToken);

        // Notifications and search indexing run from this event, outside the request.
        await _outboxWriter.AddAsync(
            new MessageCreatedIntegrationEvent(
                message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.SenderUserId,
                message.Body,
                message.CreatedAtUtc,
                ForwardedFromMessageId: null,
                ForwardedFromUserId: null,
                ForwardedFromDisplayName: null),
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsClientMessageIdConflict())
        {
            // The same client message was stored by a concurrent request: answer like a normal retry.
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            var stored = await FindByClientMessageIdAsync(command, cancellationToken)
                ?? throw new InvalidOperationException("Message could not be stored.");

            return ToDuplicateResult(stored);
        }

        return new SendMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.SenderUserId,
            message.ClientMessageId,
            message.Body,
            message.ReplyToMessageId,
            message.ReplyToSequenceNumber,
            message.CreatedAtUtc,
            false,
            ForwardedFromMessageId: null,
            ForwardedFromUserId: null,
            ForwardedFromDisplayName: null,
            createdAttachments.Select(x =>
                new SendMessageAttachmentResult(
                    x.Id,
                    x.FileName,
                    x.ContentType,
                    x.Size,
                    x.StoragePath)).ToList());
    }

    private Task<Message?> FindByClientMessageIdAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        return _dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.ClientMessageId == command.ClientMessageId,
                cancellationToken);
    }

    private static SendMessageResult ToDuplicateResult(Message message)
    {
        return new SendMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.SenderUserId,
            message.ClientMessageId,
            message.Body,
            message.ReplyToMessageId,
            message.ReplyToSequenceNumber,
            message.CreatedAtUtc,
            true,
            ForwardedFromMessageId: null,
            ForwardedFromUserId: null,
            ForwardedFromDisplayName: null,
            null);
    }
}
