namespace Messenger.Application.Files.DownloadFile;

public interface IAttachmentDownloader
{
    Task<DownloadAttachmentResult> DownloadAsync(
        DownloadAttachmentQuery query,
        CancellationToken cancellationToken);
}