namespace Messenger.Application.Files.UploadFile;

public sealed record UploadFileResult(
    Guid FileId,
    string OriginalFileName,
    string ContentType,
    long Length,
    string StoragePath
);