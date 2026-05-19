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
    List<SendMessageAttachmentResult>? Attachments
);

public sealed record SendMessageAttachmentResult(
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);