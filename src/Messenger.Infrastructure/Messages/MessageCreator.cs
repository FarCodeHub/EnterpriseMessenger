using Messenger.Application.Auditing;
using Messenger.Application.Messages.SendMessage;
using Messenger.Application.Outbox;
using Messenger.Application.Sync;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Domain.Attachments;
using Messenger.Domain.Conversations;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

/// <summary>A validated message to store; built by <see cref="MessageSender"/> or <see cref="MessageForwarder"/>.</summary>
public sealed record NewMessage(
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    IReadOnlyCollection<NewAttachment> Attachments,
    string AuditEventType,
    Func<Message, object> AuditData,
    Guid? ReplyToMessageId = null,
    long? ReplyToSequenceNumber = null,
    Guid? ForwardedFromMessageId = null,
    Guid? ForwardedFromUserId = null,
    string? ForwardedFromDisplayName = null);

public sealed record NewAttachment(string FileName, string ContentType, long Size, string StoragePath);

/// <summary>A conversation that new messages are written to, with its counter locked.</summary>
public sealed record MessageTarget(
    Conversation Conversation,
    ConversationCounter Counter,
    IReadOnlyCollection<Guid> ActiveMemberUserIds);

/// <summary>
/// The one place that creates messages. Every new message gets, in the caller's transaction: the next sequence
/// number of its conversation, its attachments, the conversation's last message, a delivery and a sync event for
/// each active member, an audit entry and a <see cref="MessageCreatedIntegrationEvent"/> in the outbox.
/// <para>
/// Use it inside a transaction: <see cref="LockCountersAsync"/>, then <see cref="PrepareTargetsAsync"/>, then
/// <see cref="CreateAsync"/> for each message; the caller saves and commits. The lock order is always conversation
/// counters (by id) and then the members' sync counters (by id), so concurrent senders and forwarders cannot
/// deadlock.
/// </para>
/// </summary>
public sealed class MessageCreator
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IOutboxWriter _outboxWriter;

    public MessageCreator(
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

    /// <summary>
    /// Locks the counters of these conversations until the transaction ends, so their messages are numbered one
    /// at a time: unique and without gaps under concurrent writes.
    /// </summary>
    public async Task<IReadOnlyList<ConversationCounter>> LockCountersAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        var counters = await _dbContext.LockConversationCountersAsync(conversationIds, cancellationToken);

        if (counters.Count != conversationIds.Distinct().Count())
            throw new InvalidOperationException("Conversation counter not found.");

        return counters;
    }

    /// <summary>
    /// Locks the sync counters of the conversations' active members and loads what <see cref="CreateAsync"/> needs.
    /// Call after <see cref="LockCountersAsync"/>.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, MessageTarget>> PrepareTargetsAsync(
        IReadOnlyCollection<ConversationCounter> lockedCounters,
        CancellationToken cancellationToken)
    {
        var conversationIds = lockedCounters.Select(x => x.ConversationId).ToList();

        await _syncEventWriter.LockMemberCountersAsync(conversationIds, cancellationToken);

        var conversations = await _dbContext.Conversations
            .Where(x => conversationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // Members who left a conversation no longer receive its messages.
        var activeMembers = await _dbContext.ConversationMembers
            .Where(x => conversationIds.Contains(x.ConversationId) && x.LeftAtUtc == null)
            .Select(x => new { x.ConversationId, x.UserId })
            .ToListAsync(cancellationToken);

        return lockedCounters.ToDictionary(
            counter => counter.ConversationId,
            counter => new MessageTarget(
                conversations.GetValueOrDefault(counter.ConversationId)
                    ?? throw new InvalidOperationException("Conversation not found."),
                counter,
                activeMembers
                    .Where(x => x.ConversationId == counter.ConversationId)
                    .Select(x => x.UserId)
                    .ToList()));
    }

    public async Task<SendMessageResult> CreateAsync(
        MessageTarget target,
        NewMessage newMessage,
        CancellationToken cancellationToken)
    {
        if (newMessage.Attachments.Any(x => string.IsNullOrWhiteSpace(x.StoragePath)))
            throw new InvalidOperationException("Attachment storage path is invalid.");

        var message = new Message(
            target.Conversation.Id,
            target.Counter.GetNextSequenceNumber(),
            newMessage.SenderUserId,
            newMessage.ClientMessageId,
            newMessage.Body,
            newMessage.ReplyToMessageId,
            newMessage.ReplyToSequenceNumber,
            newMessage.ForwardedFromMessageId,
            newMessage.ForwardedFromUserId);

        _dbContext.Messages.Add(message);

        var attachments = newMessage.Attachments
            .Select(x => new Attachment(message.Id, x.FileName, x.ContentType, x.Size, x.StoragePath))
            .ToList();

        _dbContext.Attachments.AddRange(attachments);

        target.Conversation.UpdateLastMessage(message.Id, message.CreatedAtUtc);

        foreach (var memberUserId in target.ActiveMemberUserIds)
            _dbContext.MessageDeliveries.Add(new MessageDelivery(message.Id, memberUserId));

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
                newMessage.ForwardedFromDisplayName
            },
            cancellationToken);

        await _auditLogWriter.WriteAsync(
            newMessage.AuditEventType,
            message.SenderUserId,
            "Message",
            message.Id.ToString(),
            newMessage.AuditData(message),
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
                message.ForwardedFromMessageId,
                message.ForwardedFromUserId,
                newMessage.ForwardedFromDisplayName),
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
            message.ForwardedFromMessageId,
            message.ForwardedFromUserId,
            newMessage.ForwardedFromDisplayName,
            attachments
                .Select(x => new SendMessageAttachmentResult(x.Id, x.FileName, x.ContentType, x.Size, x.StoragePath))
                .ToList());
    }
}
