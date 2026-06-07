namespace Messenger.Application.Messages.SendMessage;

public sealed record SendMessageResult(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId,
    long? ReplyToSequenceNumber,
    DateTime CreatedAtUtc,
    bool IsDuplicate,
    Guid? ForwardedFromMessageId,
    Guid? ForwardedFromUserId,
    string? ForwardedFromDisplayName,
    List<SendMessageAttachmentResult>? Attachments
);

public sealed record SendMessageAttachmentResult(
    Guid? Id,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);