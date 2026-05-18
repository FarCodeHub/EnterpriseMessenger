using Messenger.Domain.Common;

namespace Messenger.Domain.Conversations;

public class ConversationMember : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public long LastReadSequenceNumber { get; private set; }

    private ConversationMember()
    {
    }

    public ConversationMember(
        Guid conversationId,
        Guid userId)
    {
        ConversationId = conversationId;
        UserId = userId;
    }

    public void UpdateLastReadSequenceNumber(long sequenceNumber)
    {
        LastReadSequenceNumber = sequenceNumber;
    }
}