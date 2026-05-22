using Messenger.Domain.Common;

namespace Messenger.Domain.Outbox;

public class DeadLetterOutboxMessage : BaseEntity
{
    public Guid OriginalOutboxMessageId { get; private set; }

    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public string Error { get; private set; } = null!;

    public int RetryCount { get; private set; }

    public DateTime MovedAtUtc { get; private set; }

    private DeadLetterOutboxMessage()
    {
    }

    public DeadLetterOutboxMessage(
        Guid originalOutboxMessageId,
        string type,
        string payload,
        string error,
        int retryCount)
    {
        OriginalOutboxMessageId = originalOutboxMessageId;
        Type = type;
        Payload = payload;
        Error = error;
        RetryCount = retryCount;
        MovedAtUtc = DateTime.UtcNow;
    }
}