using Messenger.Application.Conversations;
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

    private readonly IConversationPermissionChecker _permissionChecker;

    public MinioAttachmentDownloadUrlGenerator(
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


        await _permissionChecker.EnsureActiveMemberAsync(
    message.ConversationId,
    query.CurrentUserId,
    cancellationToken);

        var bucketName = _configuration["Minio:BucketName"];

        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("MinIO bucket name is not configured.");

        const int expiresInSeconds = 300;

 



                var publicEndpoint = _configuration["Minio:PublicEndpoint"]
    ?? _configuration["Minio:Endpoint"]
    ?? "localhost:9000";

var accessKey = _configuration["Minio:AccessKey"] ?? "minioadmin";
var secretKey = _configuration["Minio:SecretKey"] ?? "minioadmin_password";


var publicMinioClient = new MinioClient()
    .WithEndpoint(publicEndpoint)
    .WithCredentials(accessKey, secretKey)
    .Build();


    var url = await publicMinioClient.PresignedGetObjectAsync(
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