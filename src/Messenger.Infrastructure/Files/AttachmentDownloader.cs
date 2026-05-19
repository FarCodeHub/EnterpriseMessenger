using Messenger.Application.Files.DownloadFile;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Files;

public sealed class AttachmentDownloader : IAttachmentDownloader
{
    private readonly MessengerDbContext _dbContext;

    public AttachmentDownloader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DownloadAttachmentResult> DownloadAsync(
        DownloadAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var attachment = await _dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == query.AttachmentId, cancellationToken);

        if (attachment is null)
            throw new InvalidOperationException("Attachment not found.");

        if (!File.Exists(attachment.StoragePath))
            throw new FileNotFoundException("File not found on storage.");

        var fileStream = File.OpenRead(attachment.StoragePath);

        return new DownloadAttachmentResult(
            fileStream,
            attachment.ContentType,
            attachment.FileName);
    }
}