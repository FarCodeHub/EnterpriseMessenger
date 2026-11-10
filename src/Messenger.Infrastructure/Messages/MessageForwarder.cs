using Messenger.Application.Messages.ForwardMessage;
using Messenger.Application.Messages.SendMessage;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Messenger.Domain.Messages;
using Messenger.Domain.Attachments;
using Messenger.Application.Search;
namespace Messenger.Infrastructure.Messages;

using Messenger.Application.Sync;
using Messenger.Application.Outbox;
using Messenger.Application.Auditing;
using Messenger.Application.Search;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Domain.Messages;

public sealed class MessageForwarder : IMessageForwarder
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IAuditLogWriter _auditLogWriter;

    public MessageForwarder(
        MessengerDbContext dbContext,
        IUserSyncEventWriter syncEventWriter,
        IOutboxWriter outboxWriter,
        IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
        _outboxWriter = outboxWriter;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<IReadOnlyCollection<SendMessageResult>> ForwardAsync(
        ForwardMessageCommand command,
        CancellationToken cancellationToken)
    {
        var sourceMessage = await _dbContext.Messages
    .FirstOrDefaultAsync(
        x => x.Id == command.SourceMessageId,
        cancellationToken);

        if (sourceMessage is null)
            throw new InvalidOperationException("Source message not found.");

        if (sourceMessage.DeletedAtUtc.HasValue)
            throw new InvalidOperationException(
                "Deleted messages cannot be forwarded.");


        var sourceMembershipExists =
            await _dbContext.ConversationMembers
                .AnyAsync(
                    x =>
                        x.ConversationId == sourceMessage.ConversationId &&
                        x.UserId == command.CurrentUserId &&
                        x.LeftAtUtc == null,
                    cancellationToken);

        if (!sourceMembershipExists)
            throw new UnauthorizedAccessException();

        var targetConversationIds = command.TargetConversationIds
            .Distinct()
            .ToList();

        var activeTargetMemberships =
            await _dbContext.ConversationMembers
                .Where(x =>
                    targetConversationIds.Contains(x.ConversationId) &&
                    x.UserId == command.CurrentUserId &&
                    x.LeftAtUtc == null)
                .Select(x => x.ConversationId)
                .Distinct()
                .ToListAsync(cancellationToken);

        if (activeTargetMemberships.Count != targetConversationIds.Count)
        {
            throw new UnauthorizedAccessException(
                "One or more target conversations are invalid.");
        }



        var sourceAttachments =
            await _dbContext.Attachments
                .Where(x => x.MessageId == sourceMessage.Id)
                .ToListAsync(cancellationToken);




        var results = new List<SendMessageResult>();

        var currentUser = await _dbContext.Users
            .Where(x => x.Id == command.CurrentUserId)
            .Select(x => new
            {
                x.DisplayName,
                x.UserName
            })
            .FirstOrDefaultAsync(cancellationToken);

        var originalSender = await _dbContext.Users
            .Where(x => x.Id == sourceMessage.SenderUserId)
            .Select(x => new
            {
                x.DisplayName,
                x.UserName
            })
            .FirstOrDefaultAsync(cancellationToken);

        var currentUserDisplayName =
            currentUser?.DisplayName ?? currentUser?.UserName;

        var forwardedFromDisplayName =
            originalSender?.DisplayName ?? originalSender?.UserName;



        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock all target counters (in id order) and members' sync counters before numbering messages.
        var lockedCounters = (await _dbContext.LockConversationCountersAsync(
                targetConversationIds,
                cancellationToken))
            .ToDictionary(x => x.ConversationId);

        await _syncEventWriter.LockMemberCountersAsync(targetConversationIds, cancellationToken);

        foreach (var targetConversationId in targetConversationIds)
        {
            if (!lockedCounters.TryGetValue(targetConversationId, out var counter))
                throw new InvalidOperationException("Conversation counter not found.");

            var sequenceNumber = counter.GetNextSequenceNumber();

            var message = new Message(
                targetConversationId,
                sequenceNumber,
                command.CurrentUserId,
                Guid.NewGuid(),
                sourceMessage.Body,
                replyToMessageId: null,
                replyToSequenceNumber: null,
                forwardedFromMessageId: sourceMessage.Id,
                forwardedFromUserId: sourceMessage.SenderUserId);

            _dbContext.Messages.Add(message);








            var createdAttachments = new List<Attachment>();

            foreach (var sourceAttachment in sourceAttachments)
            {
                var newAttachment = new Attachment(
                    message.Id,
                    sourceAttachment.FileName,
                    sourceAttachment.ContentType,
                    sourceAttachment.Size,
                    sourceAttachment.StoragePath);

                createdAttachments.Add(newAttachment);
                _dbContext.Attachments.Add(newAttachment);
            }


            var memberUserIds = await _dbContext.ConversationMembers
                .Where(x => x.ConversationId == targetConversationId &&
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
                    message.CreatedAtUtc,
                    message.ForwardedFromMessageId,
                    message.ForwardedFromUserId,
                    ForwardedFromDisplayName = forwardedFromDisplayName
                },
                cancellationToken);

            await _auditLogWriter.WriteAsync(
                "MessageForwarded",
                command.CurrentUserId,
                "Message",
                message.Id.ToString(),
                new
                {
                    SourceMessageId = sourceMessage.Id,
                    SourceConversationId = sourceMessage.ConversationId,
                    TargetConversationId = message.ConversationId,
                    message.SequenceNumber
                },
                cancellationToken);

            await _outboxWriter.AddAsync(
            new MessageCreatedIntegrationEvent(
                message.Id,
                message.ConversationId,
                message.SequenceNumber,
                message.SenderUserId,
                message.Body,
                message.CreatedAtUtc,
                message.ForwardedFromMessageId,
                message.ForwardedFromUserId,
                forwardedFromDisplayName),
                cancellationToken);




            var conversation = await _dbContext.Conversations
                  .FirstAsync(x => x.Id == targetConversationId, cancellationToken);

            conversation.UpdateLastMessage(message.Id, message.CreatedAtUtc);


            results.Add(new SendMessageResult(
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
                message.ForwardedFromMessageId,
                message.ForwardedFromUserId,
                forwardedFromDisplayName,
                createdAttachments.Select(x =>
                    new SendMessageAttachmentResult(
                        x.Id,
                        x.FileName,
                        x.ContentType,
                        x.Size,
                        x.StoragePath)).ToList()
          ));
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return results;

    }




    public async Task<IReadOnlyCollection<SendMessageResult>> ForwardBatchAsync(
        ForwardMessagesCommand command,
        CancellationToken cancellationToken)
    {

await using var transaction =
    await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var sourceMessageIds = command.SourceMessageIds
            .Distinct()
            .ToList();

        var targetConversationIds = command.TargetConversationIds
            .Distinct()
            .ToList();

        if (sourceMessageIds.Count == 0)
            throw new InvalidOperationException("At least one source message is required.");

        if (targetConversationIds.Count == 0)
            throw new InvalidOperationException("At least one target conversation is required.");

        var sourceMessages = await _dbContext.Messages
            .Where(x => sourceMessageIds.Contains(x.Id))
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);

        if (sourceMessages.Count != sourceMessageIds.Count)
            throw new InvalidOperationException("One or more source messages were not found.");

        if (sourceMessages.Any(x => x.DeletedAtUtc.HasValue))
            throw new InvalidOperationException("Deleted messages cannot be forwarded.");

        var sourceConversationIds = sourceMessages
            .Select(x => x.ConversationId)
            .Distinct()
            .ToList();

        if (sourceConversationIds.Count != 1)
        {
            throw new InvalidOperationException(
                "Forwarding messages from multiple conversations is not supported.");
        }

        var sourceConversationId = sourceConversationIds[0];

        var sourceMembershipExists = await _dbContext.ConversationMembers
            .AnyAsync(
                x =>
                    x.ConversationId == sourceConversationId &&
                    x.UserId == command.CurrentUserId &&
                    x.LeftAtUtc == null,
                cancellationToken);

        if (!sourceMembershipExists)
            throw new UnauthorizedAccessException();

        var activeTargetMemberships = await _dbContext.ConversationMembers
            .Where(x =>
                targetConversationIds.Contains(x.ConversationId) &&
                x.UserId == command.CurrentUserId &&
                x.LeftAtUtc == null)
            .Select(x => x.ConversationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (activeTargetMemberships.Count != targetConversationIds.Count)
        {
            throw new UnauthorizedAccessException(
                "One or more target conversations are invalid.");
        }
        var sourceAttachments = await _dbContext.Attachments
            .Where(x => sourceMessageIds.Contains(x.MessageId))
            .ToListAsync(cancellationToken);

        var attachmentsByMessageId = sourceAttachments
            .GroupBy(x => x.MessageId)
            .ToDictionary(
                x => x.Key,
                x => x.ToList());

        var originalSenderIds = sourceMessages
            .Select(x => x.SenderUserId)
            .Distinct()
            .ToList();

        var originalSenders = await _dbContext.Users
            .Where(x => originalSenderIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.UserName
            })
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);

        // Lock all target counters (in id order) and members' sync counters before numbering messages.
        var targetCounters = (await _dbContext.LockConversationCountersAsync(
                targetConversationIds,
                cancellationToken))
            .ToDictionary(x => x.ConversationId);

        await _syncEventWriter.LockMemberCountersAsync(targetConversationIds, cancellationToken);

        var targetConversations = await _dbContext.Conversations
            .Where(x => targetConversationIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);
        // مرحله بعد اینجا پیام‌های forward شده را می‌سازیم

        var results = new List<SendMessageResult>();




var conversationMembers = await _dbContext.ConversationMembers
    .Where(x =>
        targetConversationIds.Contains(x.ConversationId) &&
        x.LeftAtUtc == null)
    .ToListAsync(cancellationToken);

var membersByConversation = conversationMembers
    .GroupBy(x => x.ConversationId)
    .ToDictionary(
        x => x.Key,
        x => x.Select(y => y.UserId).ToList());

try
{
        foreach (var targetConversationId in targetConversationIds)
        {
            if (!targetCounters.TryGetValue(targetConversationId, out var counter))
                throw new InvalidOperationException("Conversation counter not found.");

            if (!targetConversations.TryGetValue(targetConversationId, out var targetConversation))
                throw new InvalidOperationException("Target conversation not found.");

            foreach (var sourceMessage in sourceMessages)
            {
                var sequenceNumber = counter.GetNextSequenceNumber();

                var message = new Message(
                    targetConversationId,
                    sequenceNumber,
                    command.CurrentUserId,
                    Guid.NewGuid(),
                    sourceMessage.Body,
                    replyToMessageId: null,
                    replyToSequenceNumber: null,
                    forwardedFromMessageId: sourceMessage.Id,
                    forwardedFromUserId: sourceMessage.SenderUserId);

                _dbContext.Messages.Add(message);

   var memberUserIds = membersByConversation[targetConversationId];

                foreach (var memberUserId in memberUserIds)
                {
                    _dbContext.MessageDeliveries.Add(
                        new MessageDelivery(message.Id, memberUserId));
                }

                var createdAttachments = new List<Attachment>();

                if (attachmentsByMessageId.TryGetValue(sourceMessage.Id, out var sourceMessageAttachments))
                {
                    foreach (var sourceAttachment in sourceMessageAttachments)
                    {
                        if (string.IsNullOrWhiteSpace(sourceAttachment.StoragePath))
                            throw new InvalidOperationException("Attachment storage path is invalid.");

                        var newAttachment = new Attachment(
                            message.Id,
                            sourceAttachment.FileName,
                            sourceAttachment.ContentType,
                            sourceAttachment.Size,
                            sourceAttachment.StoragePath);

                        createdAttachments.Add(newAttachment);
                        _dbContext.Attachments.Add(newAttachment);
                    }
                }

                targetConversation.UpdateLastMessage(
                    message.Id,
                    message.CreatedAtUtc);

                string? forwardedFromDisplayName = null;

                if (originalSenders.TryGetValue(sourceMessage.SenderUserId, out var originalSender))
                {
                    forwardedFromDisplayName =
                        originalSender.DisplayName ?? originalSender.UserName;
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
                        message.CreatedAtUtc,
                        message.ForwardedFromMessageId,
                        message.ForwardedFromUserId,
                        ForwardedFromDisplayName = forwardedFromDisplayName
                    },
                    cancellationToken);

                await _auditLogWriter.WriteAsync(
                    "MessageForwarded",
                    command.CurrentUserId,
                    "Message",
                    message.Id.ToString(),
                    new
                    {
                        SourceMessageId = sourceMessage.Id,
                        SourceConversationId = sourceMessage.ConversationId,
                        TargetConversationId = message.ConversationId,
                        message.SequenceNumber
                    },
                    cancellationToken);

                await _outboxWriter.AddAsync(
                    new MessageCreatedIntegrationEvent(
                        message.Id,
                        message.ConversationId,
                        message.SequenceNumber,
                        message.SenderUserId,
                        message.Body,
                        message.CreatedAtUtc,
                        message.ForwardedFromMessageId,
                        message.ForwardedFromUserId,
                        forwardedFromDisplayName),
                    cancellationToken);


                results.Add(new SendMessageResult(
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
                    message.ForwardedFromMessageId,
                    message.ForwardedFromUserId,
                    forwardedFromDisplayName,
                    createdAttachments.Select(x =>
                        new SendMessageAttachmentResult(
                            x.Id,
                            x.FileName,
                            x.ContentType,
                            x.Size,
                            x.StoragePath)).ToList()));

            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

         await transaction.CommitAsync(cancellationToken);
          }catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}


        return results;
    }


}