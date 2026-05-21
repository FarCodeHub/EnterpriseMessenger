using Messenger.Domain.Common;

namespace Messenger.Domain.Notifications;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }

    public Guid MessageId { get; private set; }

    public Guid ConversationId { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public string Status { get; private set; } = null!;

    private Notification()
    {
    }

    public Notification(
        Guid userId,
        Guid messageId,
        Guid conversationId,
        DateTime requestedAtUtc)
    {
        UserId = userId;
        MessageId = messageId;
        ConversationId = conversationId;
        RequestedAtUtc = requestedAtUtc;
        Status = "Pending";
    }

    public void MarkAsSent()
    {
        Status = "Sent";
        SentAtUtc = DateTime.UtcNow;
    }
}