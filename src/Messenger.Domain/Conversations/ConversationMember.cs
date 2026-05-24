using Messenger.Domain.Common;
using Messenger.Domain.Enums;

namespace Messenger.Domain.Conversations;

public class ConversationMember : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public long LastReadSequenceNumber { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? LeftAtUtc { get; private set; }
    public DateTime JoinedAtUtc { get; private set; } = DateTime.UtcNow;
    public ConversationMemberRole Role { get; private set; } = ConversationMemberRole.Member;
    private ConversationMember()
    {
    }

public ConversationMember(
    Guid conversationId,
    Guid userId,
    ConversationMemberRole role = ConversationMemberRole.Member)
{
    ConversationId = conversationId;
    UserId = userId;
    Role = role;
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


    public void Archive()
    {
        IsArchived = true;
    }

    public void Unarchive()
    {
        IsArchived = false;
    }
    public void Leave()
    {
        LeftAtUtc = DateTime.UtcNow;
    }

    public void Rejoin()
    {
        LeftAtUtc = null;
    }

    public void PromoteToAdmin()
{
    Role = ConversationMemberRole.Admin;
}

public void DemoteToMember()
{
    Role = ConversationMemberRole.Member;
}
public void MarkAsRead(long sequenceNumber)
{
    if (sequenceNumber > LastReadSequenceNumber)
    {
        LastReadSequenceNumber = sequenceNumber;
    }
}
}