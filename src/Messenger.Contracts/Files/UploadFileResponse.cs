namespace Messenger.Contracts.Files;

public sealed record UploadFileResponse(
    Guid FileId,
    string FileName,
    string ContentType,
    long Size,
    string StoragePath
);