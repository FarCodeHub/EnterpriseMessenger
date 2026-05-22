namespace Messenger.Application.Files.GetDownloadUrl;

public sealed record GetAttachmentDownloadUrlQuery(
    Guid AttachmentId,
    Guid CurrentUserId
);