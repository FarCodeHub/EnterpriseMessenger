using System.Diagnostics;
using Messenger.Application.Messages.SendMessage;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Observability;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Conversations;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageSender : IMessageSender
{
    private readonly MessengerDbContext _dbContext;
    private readonly MessageCreator _messageCreator;
    private readonly IConversationPermissionChecker _permissionChecker;

    public MessageSender(
        MessengerDbContext dbContext,
        MessageCreator messageCreator,
        IConversationPermissionChecker permissionChecker)
    {
        _dbContext = dbContext;
        _messageCreator = messageCreator;
        _permissionChecker = permissionChecker;
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();

        var result = await StoreAsync(command, cancellationToken);

        MessengerMetrics.SendDuration.Record(
            MessengerMetrics.ElapsedSeconds(start),
            new KeyValuePair<string, object?>("result", result.IsDuplicate ? "duplicate" : "created"));

        if (!result.IsDuplicate)
            MessengerMetrics.MessagesCreated.Add(1, new KeyValuePair<string, object?>("kind", "send"));

        return result;
    }

    private async Task<SendMessageResult> StoreAsync(
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

        var counters = await _messageCreator.LockCountersAsync([command.ConversationId], cancellationToken);

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

        var attachments = await LoadOwnUploadsAsync(command, cancellationToken);

        var target = (await _messageCreator.PrepareTargetsAsync(counters, cancellationToken))[command.ConversationId];

        var result = await _messageCreator.CreateAsync(
            target,
            new NewMessage(
                command.SenderUserId,
                command.ClientMessageId,
                command.Body,
                attachments,
                "MessageSent",
                message => new
                {
                    message.ConversationId,
                    message.SequenceNumber,
                    message.ClientMessageId
                },
                command.ReplyToMessageId,
                replyToSequenceNumber),
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

        return result;
    }

    // Only files uploaded by the sender can be attached; metadata comes from the upload record.
    private async Task<IReadOnlyCollection<NewAttachment>> LoadOwnUploadsAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Attachments is null || command.Attachments.Count == 0)
            return [];

        var fileIds = command.Attachments
            .Select(x => x.FileId)
            .Distinct()
            .ToList();

        var uploadedFiles = await _dbContext.UploadedFiles
            .Where(x =>
                fileIds.Contains(x.Id) &&
                x.UploaderUserId == command.SenderUserId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return command.Attachments
            .Select(x => uploadedFiles.GetValueOrDefault(x.FileId)
                ?? throw new InvalidOperationException("Attachment file not found."))
            .Select(x => new NewAttachment(x.FileName, x.ContentType, x.Size, x.StoragePath))
            .ToList();
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
