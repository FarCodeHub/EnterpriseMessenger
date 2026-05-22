using Messenger.Application.Files.DownloadFile;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace Messenger.Infrastructure.Files;

public sealed class AttachmentDownloader : IAttachmentDownloader
{
    private readonly MessengerDbContext _dbContext;
    private readonly IMinioClient _minioClient;
    private readonly IConfiguration _configuration;

    public AttachmentDownloader(
        MessengerDbContext dbContext,
        IMinioClient minioClient,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _minioClient = minioClient;
        _configuration = configuration;
    }

    public async Task<DownloadAttachmentResult> DownloadAsync(
        DownloadAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var attachment = await _dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == query.AttachmentId, cancellationToken);

        if (attachment is null)
            throw new InvalidOperationException("Attachment not found.");

        var bucketName = _configuration["Minio:BucketName"];

        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("MinIO bucket name is not configured.");

        var memoryStream = new MemoryStream();

        await _minioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(attachment.StoragePath)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                }),
            cancellationToken);

        memoryStream.Position = 0;

        return new DownloadAttachmentResult(
            memoryStream,
            attachment.ContentType,
            attachment.FileName);
    }
}