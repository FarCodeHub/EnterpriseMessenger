namespace Messenger.Application.Files.DownloadFile;

public sealed record DownloadAttachmentQuery(
    Guid AttachmentId,
    Guid CurrentUserId
);