namespace Messenger.Application.Messages.GetMessages;

public sealed record MessageListItemResult(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    string Body,
    Guid? ReplyToMessageId,
    long? ReplyToSequenceNumber,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    IReadOnlyCollection<MessageAttachmentResult> Attachments
);