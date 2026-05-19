namespace Messenger.Application.Messages.GetMessages;

public sealed record MessageAttachmentResult(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);