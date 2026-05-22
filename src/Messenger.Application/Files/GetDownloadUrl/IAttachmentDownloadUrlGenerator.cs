namespace Messenger.Application.Files.GetDownloadUrl;

public interface IAttachmentDownloadUrlGenerator
{
    Task<GetAttachmentDownloadUrlResult> GenerateAsync(
        GetAttachmentDownloadUrlQuery query,
        CancellationToken cancellationToken);
}