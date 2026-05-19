using Messenger.Application.Files.UploadFile;
using Microsoft.Extensions.Configuration;

namespace Messenger.Infrastructure.Files;

public sealed class LocalFileUploader : IFileUploader
{
    private readonly IConfiguration _configuration;

    public LocalFileUploader(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<UploadFileResult> UploadAsync(
        UploadFileCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Size == 0)
            throw new InvalidOperationException("File is empty.");

        var rootPath = _configuration["FileStorage:RootPath"];

        if (string.IsNullOrWhiteSpace(rootPath))
            throw new InvalidOperationException("File storage root path is not configured.");

        Directory.CreateDirectory(rootPath);

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(command.FileName);
        var storedFileName = $"{fileId}{extension}";
        var storagePath = Path.Combine(rootPath, storedFileName);

        await using var stream = File.Create(storagePath);
        await command.Content.CopyToAsync(stream, cancellationToken);

        return new UploadFileResult(
            fileId,
            command.FileName,
            command.ContentType,
            command.Size,
            storagePath);
    }
}