using Messenger.Application.Files.UploadFile;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace Messenger.Infrastructure.Files;

public sealed class MinioFileUploader : IFileUploader
{
    private readonly IMinioClient _minioClient;
    private readonly IConfiguration _configuration;

    public MinioFileUploader(
        IMinioClient minioClient,
        IConfiguration configuration)
    {
        _minioClient = minioClient;
        _configuration = configuration;
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

        await _minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(command.Content)
                .WithObjectSize(command.Size)
                .WithContentType(command.ContentType),
            cancellationToken);

        return new UploadFileResult(
            fileId,
            command.FileName,
            command.ContentType,
            command.Size,
            objectName);
    }
}