namespace Messenger.Application.Files.UploadFile;

public interface IFileUploader
{
    Task<UploadFileResult> UploadAsync(
        UploadFileCommand command,
        CancellationToken cancellationToken);
}