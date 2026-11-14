using Messenger.Application.Messages.ForwardMessage;
using Messenger.Application.Messages.SendMessage;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageForwarder : IMessageForwarder
{
    private readonly MessengerDbContext _dbContext;
    private readonly MessageCreator _messageCreator;

    public MessageForwarder(
        MessengerDbContext dbContext,
        MessageCreator messageCreator)
    {
        _dbContext = dbContext;
        _messageCreator = messageCreator;
    }

    public Task<IReadOnlyCollection<SendMessageResult>> ForwardAsync(
        ForwardMessageCommand command,
        CancellationToken cancellationToken)
    {
        return ForwardBatchAsync(
            new ForwardMessagesCommand(
                [command.SourceMessageId],
                command.CurrentUserId,
                command.TargetConversationIds,
                command.Comment),
            cancellationToken);
    }

    /// <summary>
    /// Copies the source messages (all from one conversation, kept in their order) into every target conversation.
    /// The new messages are sent by the current user and remember the original message and its sender.
    /// </summary>
    public async Task<IReadOnlyCollection<SendMessageResult>> ForwardBatchAsync(
        ForwardMessagesCommand command,
        CancellationToken cancellationToken)
    {
        var sourceMessageIds = command.SourceMessageIds.Distinct().ToList();
        var targetConversationIds = command.TargetConversationIds.Distinct().ToList();

        if (sourceMessageIds.Count == 0)
            throw new InvalidOperationException("At least one source message is required.");

        if (targetConversationIds.Count == 0)
            throw new InvalidOperationException("At least one target conversation is required.");

        var sourceMessages = await _dbContext.Messages
            .AsNoTracking()
            .Where(x => sourceMessageIds.Contains(x.Id))
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);

        if (sourceMessages.Count != sourceMessageIds.Count)
            throw new InvalidOperationException("One or more source messages were not found.");

        if (sourceMessages.Any(x => x.DeletedAtUtc.HasValue))
            throw new InvalidOperationException("Deleted messages cannot be forwarded.");

        var sourceConversationIds = sourceMessages.Select(x => x.ConversationId).Distinct().ToList();

        if (sourceConversationIds.Count != 1)
        {
            throw new InvalidOperationException(
                "Forwarding messages from multiple conversations is not supported.");
        }

        var sourceConversationId = sourceConversationIds[0];

        if (!await IsActiveMemberAsync([sourceConversationId], command.CurrentUserId, cancellationToken))
            throw new UnauthorizedAccessException();

        if (!await IsActiveMemberAsync(targetConversationIds, command.CurrentUserId, cancellationToken))
            throw new UnauthorizedAccessException("One or more target conversations are invalid.");

        var attachmentsByMessageId = (await _dbContext.Attachments
                .AsNoTracking()
                .Where(x => sourceMessageIds.Contains(x.MessageId))
                .ToListAsync(cancellationToken))
            .ToLookup(
                x => x.MessageId,
                x => new NewAttachment(x.FileName, x.ContentType, x.Size, x.StoragePath));

        var originalSenderIds = sourceMessages.Select(x => x.SenderUserId).Distinct().ToList();

        var originalSenderNames = await _dbContext.Users
            .Where(x => originalSenderIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.DisplayName ?? x.UserName,
                cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var counters = await _messageCreator.LockCountersAsync(targetConversationIds, cancellationToken);
        var targets = await _messageCreator.PrepareTargetsAsync(counters, cancellationToken);

        var results = new List<SendMessageResult>();

        foreach (var targetConversationId in targetConversationIds)
        {
            foreach (var sourceMessage in sourceMessages)
            {
                results.Add(await _messageCreator.CreateAsync(
                    targets[targetConversationId],
                    new NewMessage(
                        command.CurrentUserId,
                        Guid.NewGuid(),
                        sourceMessage.Body,
                        attachmentsByMessageId[sourceMessage.Id].ToList(),
                        "MessageForwarded",
                        message => new
                        {
                            SourceMessageId = sourceMessage.Id,
                            SourceConversationId = sourceMessage.ConversationId,
                            TargetConversationId = message.ConversationId,
                            message.SequenceNumber
                        },
                        ForwardedFromMessageId: sourceMessage.Id,
                        ForwardedFromUserId: sourceMessage.SenderUserId,
                        ForwardedFromDisplayName: originalSenderNames.GetValueOrDefault(sourceMessage.SenderUserId)),
                    cancellationToken));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return results;
    }

    /// <summary>True when the user is an active member of every one of these conversations.</summary>
    private async Task<bool> IsActiveMemberAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var memberOf = await _dbContext.ConversationMembers
            .Where(x =>
                conversationIds.Contains(x.ConversationId) &&
                x.UserId == userId &&
                x.LeftAtUtc == null)
            .Select(x => x.ConversationId)
            .Distinct()
            .CountAsync(cancellationToken);

        return memberOf == conversationIds.Count;
    }
}
