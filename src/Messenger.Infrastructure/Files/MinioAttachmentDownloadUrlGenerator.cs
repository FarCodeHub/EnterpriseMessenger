using Messenger.Application.Files.GetDownloadUrl;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace Messenger.Infrastructure.Files;

public sealed class MinioAttachmentDownloadUrlGenerator : IAttachmentDownloadUrlGenerator
{
    private readonly MessengerDbContext _dbContext;
    private readonly IMinioClient _minioClient;
    private readonly IConfiguration _configuration;

    public MinioAttachmentDownloadUrlGenerator(
        MessengerDbContext dbContext,
        IMinioClient minioClient,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _minioClient = minioClient;
        _configuration = configuration;
    }

    public async Task<GetAttachmentDownloadUrlResult> GenerateAsync(
        GetAttachmentDownloadUrlQuery query,
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

        var isMember = await _dbContext.ConversationMembers
            .AnyAsync(x =>
                x.ConversationId == message.ConversationId &&
                x.UserId == query.CurrentUserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (!isMember)
            throw new UnauthorizedAccessException("You do not have access to this attachment.");

        var bucketName = _configuration["Minio:BucketName"];

        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("MinIO bucket name is not configured.");

        const int expiresInSeconds = 300;

        var url = await _minioClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(attachment.StoragePath)
                .WithExpiry(expiresInSeconds));

        return new GetAttachmentDownloadUrlResult(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            url,
            expiresInSeconds);
    }
}