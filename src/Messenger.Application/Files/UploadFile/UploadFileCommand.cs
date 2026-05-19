namespace Messenger.Application.Files.UploadFile;

public sealed record UploadFileCommand(
    string FileName,
    string ContentType,
    long Size,
    Stream Content
);