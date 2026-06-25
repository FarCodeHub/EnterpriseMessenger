namespace Messenger.Application.Messages.GetMessages;

public sealed record MessageListItemResult(
    Guid Id,
    Guid ConversationId,
    Guid? ClientMessageId,
    long SequenceNumber,
    Guid SenderUserId,
    string Body,
    Guid? ReplyToMessageId,
    long? ReplyToSequenceNumber,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    Guid? ForwardedFromMessageId,
Guid? ForwardedFromUserId,
string? ForwardedFromDisplayName,
    IReadOnlyCollection<MessageAttachmentResult> Attachments
);