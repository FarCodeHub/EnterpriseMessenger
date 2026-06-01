using Messenger.Application.Messages.SendMessage;
using Messenger.Application.Sync;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Application.Auditing;
using Messenger.Application.Outbox;
using Messenger.Application.Conversations;
using Messenger.Application.Search;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageSender : IMessageSender
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IConversationPermissionChecker _permissionChecker;
    private readonly IMessageSearchIndexer _messageSearchIndexer;

    public MessageSender(
        MessengerDbContext dbContext,
        IUserSyncEventWriter syncEventWriter,

        IAuditLogWriter auditLogWriter,
        IOutboxWriter outboxWriter,
        IConversationPermissionChecker permissionChecker,
        IMessageSearchIndexer messageSearchIndexer)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;

        _auditLogWriter = auditLogWriter;
        _outboxWriter = outboxWriter;
        _permissionChecker = permissionChecker;
        _messageSearchIndexer = messageSearchIndexer;
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
var hasBody = !string.IsNullOrWhiteSpace(command.Body);

var hasAttachments =
    command.Attachments?.Any() == true;

if (!hasBody && !hasAttachments)
{
    throw new InvalidOperationException(
        "Message must contain text or attachment.");
}


        await _permissionChecker.EnsureActiveMemberAsync(
            command.ConversationId,
            command.SenderUserId,
            cancellationToken);



        var duplicateMessage = await _dbContext.Messages
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.ClientMessageId == command.ClientMessageId,
                cancellationToken);

        if (duplicateMessage is not null)
        {
            return new SendMessageResult(
                duplicateMessage.Id,
                duplicateMessage.ConversationId,
                duplicateMessage.SequenceNumber,
                duplicateMessage.SenderUserId,
                duplicateMessage.ClientMessageId,
                duplicateMessage.Body,
                duplicateMessage.ReplyToMessageId,
                duplicateMessage.ReplyToSequenceNumber,
                duplicateMessage.CreatedAtUtc,
                true,
                null);
        }

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

        var counter = await _dbContext.ConversationCounters
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId,
                cancellationToken);

        if (counter is null)
            throw new InvalidOperationException("Conversation counter not found.");

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
    foreach (var attachment in command.Attachments)
    {
        var createdAttachment = new Messenger.Domain.Attachments.Attachment(
            message.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Size,
            attachment.StoragePath);

        createdAttachments.Add(createdAttachment);
        _dbContext.Attachments.Add(createdAttachment);
    }
}

        var conversation = await _dbContext.Conversations
            .FirstAsync(x => x.Id == command.ConversationId, cancellationToken);

        conversation.UpdateLastMessage(message.Id, message.CreatedAtUtc);

        var memberUserIds = await _dbContext.ConversationMembers
            .Where(x => x.ConversationId == command.ConversationId)
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


        await _outboxWriter.AddAsync(
            new MessageCreatedIntegrationEvent(
                message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.SenderUserId,
                message.Body,
                message.CreatedAtUtc),
            cancellationToken);



        await _dbContext.SaveChangesAsync(cancellationToken);



await _messageSearchIndexer.IndexAsync(
    new MessageSearchDocument(
        message.Id,
        message.ConversationId,
        message.SenderUserId,
        message.Body,
        message.SequenceNumber,
        message.CreatedAtUtc,
        EditedAtUtc: null,
        IsDeleted: false),
    cancellationToken);


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
    createdAttachments.Select(x =>
        new SendMessageAttachmentResult(
            x.Id,
            x.FileName,
            x.ContentType,
            x.Size,
            x.StoragePath)).ToList());
    }






}