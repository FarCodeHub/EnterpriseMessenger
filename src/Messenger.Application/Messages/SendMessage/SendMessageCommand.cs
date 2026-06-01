namespace Messenger.Application.Messages.SendMessage;

public sealed record SendMessageCommand(
    Guid ConversationId,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId,
    List<SendMessageAttachmentCommand>? Attachments
);

public sealed record SendMessageAttachmentCommand(
    Guid? Id,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);