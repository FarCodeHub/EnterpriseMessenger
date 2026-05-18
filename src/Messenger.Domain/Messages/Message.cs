using Messenger.Domain.Common;

namespace Messenger.Domain.Messages;

public class Message : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public long SequenceNumber { get; private set; }

    public Guid SenderUserId { get; private set; }

    public Guid ClientMessageId { get; private set; }

    public Guid? ReplyToMessageId { get; private set; }

    public long? ReplyToSequenceNumber { get; private set; }

    public string Body { get; private set; }

    private Message()
    {
    }

    public Message(
        Guid conversationId,
        long sequenceNumber,
        Guid senderUserId,
        Guid clientMessageId,
        string body,
        Guid? replyToMessageId = null,
        long? replyToSequenceNumber = null)
    {
        ConversationId = conversationId;
        SequenceNumber = sequenceNumber;
        SenderUserId = senderUserId;
        ClientMessageId = clientMessageId;
        Body = body;

        ReplyToMessageId = replyToMessageId;
        ReplyToSequenceNumber = replyToSequenceNumber;
    }
}