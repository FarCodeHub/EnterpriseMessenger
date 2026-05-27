namespace Messenger.Application.Files.GetDownloadUrl;

public sealed record GetAttachmentDownloadUrlResult(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    string Url,
    int ExpiresInSeconds
);