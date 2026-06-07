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
 
    private readonly IMessageSearchIndexer _messageSearchIndexer;
public MessageForwarder(
    MessengerDbContext dbContext,
    IUserSyncEventWriter syncEventWriter,
    IOutboxWriter outboxWriter,
    IAuditLogWriter auditLogWriter,
    IMessageSearchIndexer messageSearchIndexer)
{
    _dbContext = dbContext;
    _syncEventWriter = syncEventWriter;
    _outboxWriter = outboxWriter;
    _auditLogWriter = auditLogWriter;
    _messageSearchIndexer = messageSearchIndexer;
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
        var searchDocuments = new List<MessageSearchDocument>();

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



        foreach (var targetConversationId in targetConversationIds)
        {
            var counter = await _dbContext.ConversationCounters
                .FirstOrDefaultAsync(
                    x => x.ConversationId == targetConversationId,
                    cancellationToken);

            if (counter is null)
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




            searchDocuments.Add(new MessageSearchDocument(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.Body,
                message.SequenceNumber,
                message.CreatedAtUtc,
                message.EditedAtUtc,
                IsDeleted: false));




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
    .Where(x => x.ConversationId == targetConversationId&&
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
        foreach (var document in searchDocuments)
        {
            await _messageSearchIndexer.IndexAsync(
                document,
                cancellationToken);
        }
        return results;

    }







}