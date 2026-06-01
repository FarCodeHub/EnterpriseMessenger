namespace Messenger.Contracts.Messages;

public sealed record SendMessageRequest(
    Guid ConversationId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId = null,
    List<SendMessageAttachmentRequest>? Attachments = null
);

public sealed record SendMessageAttachmentRequest(
    Guid? Id,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);