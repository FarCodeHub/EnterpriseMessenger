namespace Messenger.Application.Files.DownloadFile;

public sealed record DownloadAttachmentResult(
    Stream Content,
    string ContentType,
    string FileName
);