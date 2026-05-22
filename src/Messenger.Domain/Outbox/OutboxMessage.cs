using Messenger.Domain.Common;

namespace Messenger.Domain.Outbox;

public class OutboxMessage : BaseEntity
{
    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(string type, string payload)
    {
        Type = type;
        Payload = payload;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public void MarkAsProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        Error = null;
    }

public void MarkAsFailed(string error)
{
    RetryCount++;
    Error = error;
}
}