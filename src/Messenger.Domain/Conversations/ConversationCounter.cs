using Messenger.Domain.Common;

namespace Messenger.Domain.Conversations;

public class ConversationCounter : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public long LastSequenceNumber { get; private set; }

    private ConversationCounter()
    {
    }

    public ConversationCounter(Guid conversationId)
    {
        ConversationId = conversationId;
        LastSequenceNumber = 0;
    }

    public long GetNextSequenceNumber()
    {
        LastSequenceNumber++;
        return LastSequenceNumber;
    }
}