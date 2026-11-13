namespace Messenger.Application.Files.UploadFile;

public sealed record UploadFileCommand(
    Guid UploaderUserId,
    string FileName,
    string ContentType,
    long Size,
    Stream Content
);