namespace Messenger.Contracts.Messages;

public sealed record SendMessageRequest(
    Guid ConversationId,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId = null,
    List<SendMessageAttachmentRequest>? Attachments = null
);

public sealed record SendMessageAttachmentRequest(
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);