using Messenger.Domain.Common;

namespace Messenger.Domain.Conversations;

public class ConversationMember : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public long LastReadSequenceNumber { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsPinned { get; private set; }

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
    public void Mute()
    {
        IsMuted = true;
    }

    public void Unmute()
    {
        IsMuted = false;
    }
    public void Pin()
{
    IsPinned = true;
}

public void Unpin()
{
    IsPinned = false;
}
}