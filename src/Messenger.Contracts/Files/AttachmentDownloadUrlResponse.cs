namespace Messenger.Contracts.Files;

public sealed record AttachmentDownloadUrlResponse(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    string Url,
    int ExpiresInSeconds
);