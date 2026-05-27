using Messenger.Application.Conversations;
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

    private readonly IConversationPermissionChecker _permissionChecker;

    public AttachmentDownloader(
        MessengerDbContext dbContext,
        IMinioClient minioClient,
        IConfiguration configuration,
        IConversationPermissionChecker permissionChecker)
    {
        _dbContext = dbContext;
        _minioClient = minioClient;
        _configuration = configuration;
        _permissionChecker = permissionChecker;
    }

    public async Task<DownloadAttachmentResult> DownloadAsync(
        DownloadAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var attachment = await _dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == query.AttachmentId, cancellationToken);

        if (attachment is null)
            throw new InvalidOperationException("Attachment not found.");

        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == attachment.MessageId, cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

      await _permissionChecker.EnsureActiveMemberAsync(
    message.ConversationId,
    query.CurrentUserId,
    cancellationToken);

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