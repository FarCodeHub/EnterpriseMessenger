using Messenger.Domain.Common;
using Messenger.Domain.Attachments;
namespace Messenger.Domain.Attachments;

public class Attachment : BaseEntity
{
    public Guid MessageId { get; private set; }

    public string FileName { get; private set; }= null!;

    public string ContentType { get; private set; }= null!;

    public long Size { get; private set; }

    public string StoragePath { get; private set; }= null!;

    private Attachment()
    {
    }

    public Attachment(
        Guid messageId,
        string fileName,
        string contentType,
        long size,
        string storagePath)
    {
        MessageId = messageId;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        StoragePath = storagePath;
    }
}