using Messenger.Application.Files.UploadFile;
using Messenger.Domain.Attachments;
using Messenger.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace Messenger.Infrastructure.Files;

public sealed class MinioFileUploader : IFileUploader
{
    private readonly IMinioClient _minioClient;
    private readonly IConfiguration _configuration;
    private readonly MessengerDbContext _dbContext;

    public MinioFileUploader(
        IMinioClient minioClient,
        IConfiguration configuration,
        MessengerDbContext dbContext)
    {
        _minioClient = minioClient;
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<UploadFileResult> UploadAsync(
        UploadFileCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Size == 0)
            throw new InvalidOperationException("File is empty.");

        var bucketName = _configuration["Minio:BucketName"];

        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("MinIO bucket name is not configured.");

        var bucketExists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs()
                .WithBucket(bucketName),
            cancellationToken);

        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs()
                    .WithBucket(bucketName),
                cancellationToken);
        }

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(command.FileName);

        var objectName =
            $"attachments/{DateTime.UtcNow:yyyy/MM/dd}/{fileId}{extension}";



        var maxFileSize = long.TryParse(
            _configuration["FileUpload:MaxFileSizeBytes"],
            out var configuredMaxFileSize)
            ? configuredMaxFileSize
            : 50_000_000;

        if (command.Size > maxFileSize)
            throw new InvalidOperationException("File size exceeds the allowed limit.");

        var allowedContentTypes = _configuration
            .GetSection("FileUpload:AllowedContentTypes")
            .Get<string[]>() ?? [];

        if (allowedContentTypes.Length > 0 &&
            !allowedContentTypes.Contains(command.ContentType))
        {
            throw new InvalidOperationException("File type is not allowed.");
        }




        await _minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(command.Content)
                .WithObjectSize(command.Size)
                .WithContentType(command.ContentType),
            cancellationToken);

        // Remember who uploaded the file: messages may only attach files their sender uploaded.
        _dbContext.UploadedFiles.Add(new UploadedFile(
            fileId,
            command.UploaderUserId,
            command.FileName,
            command.ContentType,
            command.Size,
            objectName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UploadFileResult(
            fileId,
            command.FileName,
            command.ContentType,
            command.Size,
            objectName);
    }
}