using Messenger.Domain.Common;

namespace Messenger.Domain.Messages;

public class MessageDelivery : BaseEntity
{
    public Guid MessageId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public DateTime? SeenAtUtc { get; private set; }

    private MessageDelivery()
    {
    }

    public MessageDelivery(Guid messageId, Guid userId)
    {
        MessageId = messageId;
        UserId = userId;
    }

    public void MarkDelivered()
    {
        DeliveredAtUtc ??= DateTime.UtcNow;
    }

    public void MarkSeen()
    {
        SeenAtUtc ??= DateTime.UtcNow;
        DeliveredAtUtc ??= DateTime.UtcNow;
    }
}