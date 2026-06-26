using Messenger.Application.Messages.GetMessages;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageReader : IMessageReader
{
    private readonly MessengerDbContext _dbContext;

    public MessageReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<MessageListItemResult>> GetConversationMessagesAsync(
        Guid conversationId,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);

        var query = _dbContext.Messages
            .Where(x => x.ConversationId == conversationId);

        if (beforeSequence is not null)
            query = query.Where(x => x.SequenceNumber < beforeSequence.Value);

        var messages = await query
            .OrderByDescending(x => x.SequenceNumber)
            .Take(take)
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.ClientMessageId,
                x.SequenceNumber,
                x.SenderUserId,
                x.Body,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber,
                x.CreatedAtUtc,
                x.EditedAtUtc,
                x.DeletedAtUtc,
                x.ForwardedFromMessageId,
                x.ForwardedFromUserId
            })
            .ToListAsync(cancellationToken);

        var messageIds = messages
            .Select(x => x.Id)
            .ToList();

        var attachmentsByMessageId = await _dbContext.Attachments
            .Where(a => messageIds.Contains(a.MessageId))
            .Select(a => new
            {
                a.MessageId,
                Attachment = new MessageAttachmentResult(
                    a.Id,
                    a.FileName,
                    a.ContentType,
                    a.Size,
                    a.StoragePath)
            })
            .GroupBy(x => x.MessageId)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Select(y => y.Attachment).ToList(),
                cancellationToken);

        var forwardedFromUserIds = messages
            .Where(x => x.ForwardedFromUserId.HasValue)
            .Select(x => x.ForwardedFromUserId!.Value)
            .Distinct()
            .ToList();

        var forwardedUsers = await _dbContext.Users
            .Where(x => forwardedFromUserIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.UserName
            })
            .ToDictionaryAsync(
                x => x.Id,
                x => x,
                cancellationToken);

        return messages
            .Select(x =>
            {
                string? forwardedFromDisplayName = null;

                if (x.ForwardedFromUserId.HasValue &&
                    forwardedUsers.TryGetValue(x.ForwardedFromUserId.Value, out var forwardedUser))
                {
                    forwardedFromDisplayName =
                        forwardedUser.DisplayName ?? forwardedUser.UserName;
                }

                attachmentsByMessageId.TryGetValue(
                    x.Id,
                    out var attachments);

                return new MessageListItemResult(
                    x.Id,
                    x.ConversationId,
                    x.ClientMessageId,
                    x.SequenceNumber,
                    x.SenderUserId,
                    x.Body,
                    x.ReplyToMessageId,
                    x.ReplyToSequenceNumber,
                    x.CreatedAtUtc,
                    x.EditedAtUtc,
                    x.DeletedAtUtc,
                    x.ForwardedFromMessageId,
                    x.ForwardedFromUserId,
                    forwardedFromDisplayName,
                    attachments ?? []);
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<MessageListItemResult>> GetMessagesAroundAsync(
        Guid conversationId,
        long sequenceNumber,
        int radius,
        CancellationToken cancellationToken)
    {
        radius = Math.Clamp(radius, 10, 200);

        var from = sequenceNumber - radius;
        var to = sequenceNumber + radius;

        return await _dbContext.Messages
            .Where(x =>
                x.ConversationId == conversationId &&
                x.SequenceNumber >= from &&
                x.SequenceNumber <= to)
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new MessageListItemResult(
                x.Id,
                x.ConversationId,
                x.ClientMessageId,
                x.SequenceNumber,
                x.SenderUserId,
                x.Body,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber,
                x.CreatedAtUtc,
                x.EditedAtUtc,
                x.DeletedAtUtc,
                ForwardedFromMessageId: null,
ForwardedFromUserId: null,
ForwardedFromDisplayName: null,
                _dbContext.Attachments
                    .Where(a => a.MessageId == x.Id)
                    .Select(a => new MessageAttachmentResult(
                        a.Id,
                        a.FileName,
                        a.ContentType,
                        a.Size,
                        a.StoragePath))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<object> GetReplyContextAsync(
        Guid messageId,
        int radius,
        CancellationToken cancellationToken)
    {
        radius = Math.Clamp(radius, 10, 200);

        var message = await _dbContext.Messages
            .Where(x => x.Id == messageId)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.ReplyToSequenceNumber is null)
            throw new InvalidOperationException("This message is not a reply.");

        var messages = await GetMessagesAroundAsync(
            message.ConversationId,
            message.ReplyToSequenceNumber.Value,
            radius,
            cancellationToken);

        return new
        {
            message.Id,
            message.ConversationId,
            TargetMessageId = message.ReplyToMessageId,
            TargetSequenceNumber = message.ReplyToSequenceNumber,
            Radius = radius,
            Messages = messages.Select(x => new
            {
                x.Id,
                x.ConversationId,
                x.SequenceNumber,
                x.SenderUserId,
                x.Body,
                x.ReplyToMessageId,
                x.ReplyToSequenceNumber,
                x.CreatedAtUtc,
                x.EditedAtUtc,
                x.DeletedAtUtc,
                x.Attachments,
                IsTarget = x.Id == message.ReplyToMessageId
            })
        };
    }
}