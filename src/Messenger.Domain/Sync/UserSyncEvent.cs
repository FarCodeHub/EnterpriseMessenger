using Messenger.Domain.Common;

namespace Messenger.Domain.Sync;

public class UserSyncEvent : BaseEntity
{
    public long EventId { get; private set; }

    public Guid UserId { get; private set; }

    public string EventType { get; private set; }

    public Guid? ConversationId { get; private set; }

    public Guid? MessageId { get; private set; }

    public string PayloadJson { get; private set; }

    private UserSyncEvent()
    {
    }

    public UserSyncEvent(
        long eventId,
        Guid userId,
        string eventType,
        string payloadJson,
        Guid? conversationId = null,
        Guid? messageId = null)
    {
        EventId = eventId;
        UserId = userId;
        EventType = eventType;
        PayloadJson = payloadJson;

        ConversationId = conversationId;
        MessageId = messageId;
    }
}