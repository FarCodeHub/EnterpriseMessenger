using Messenger.Domain.Common;

namespace Messenger.Domain.Attachments;

/// <summary>
/// A file stored by the upload endpoint. Messages reference it by <see cref="BaseEntity.Id"/>;
/// the storage path and metadata always come from this record, never from the client.
/// </summary>
public class UploadedFile : BaseEntity
{
    public Guid UploaderUserId { get; private set; }

    public string FileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long Size { get; private set; }

    public string StoragePath { get; private set; } = null!;

    private UploadedFile()
    {
    }

    public UploadedFile(
        Guid id,
        Guid uploaderUserId,
        string fileName,
        string contentType,
        long size,
        string storagePath)
    {
        Id = id;
        UploaderUserId = uploaderUserId;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        StoragePath = storagePath;
    }
}
