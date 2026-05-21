using Messenger.Application.Messages.SendMessage;
using Messenger.Application.Sync;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Application.Auditing;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageSender : IMessageSender
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAuditLogWriter _auditLogWriter;

    public MessageSender(
        MessengerDbContext dbContext,
        IUserSyncEventWriter syncEventWriter,
        IPublishEndpoint publishEndpoint,
        IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
        _publishEndpoint = publishEndpoint;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Body))
            throw new InvalidOperationException("Message body is required.");

        var isMember = await _dbContext.ConversationMembers
            .AnyAsync(x =>
                x.ConversationId == command.ConversationId && x.LeftAtUtc == null &&
                x.UserId == command.SenderUserId,
                cancellationToken);

        if (!isMember)
            throw new InvalidOperationException("Sender is not a member of this conversation.");

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

        if (command.Attachments is not null && command.Attachments.Count > 0)
        {
            foreach (var attachment in command.Attachments)
            {
                _dbContext.Attachments.Add(
                    new Messenger.Domain.Attachments.Attachment(
                        message.Id,
                        attachment.FileName,
                        attachment.ContentType,
                        attachment.Size,
                        attachment.StoragePath));
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


        await _dbContext.SaveChangesAsync(cancellationToken);
        await _publishEndpoint.Publish(
            new MessageCreatedIntegrationEvent(
                message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.SenderUserId,
                message.Body,
                message.CreatedAtUtc),
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
            command.Attachments?.Select(x =>
                new SendMessageAttachmentResult(
                    x.FileName,
                    x.ContentType,
                    x.Size,
                    x.StoragePath)).ToList());
    }






}