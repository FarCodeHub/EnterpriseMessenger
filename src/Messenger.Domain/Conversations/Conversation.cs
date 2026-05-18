using Messenger.Domain.Common;
using Messenger.Domain.Enums;

namespace Messenger.Domain.Conversations;

public class Conversation : BaseEntity
{
    public ConversationType Type { get; private set; }

    public string? Title { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    private Conversation()
    {
    }

    public Conversation(
        ConversationType type,
        Guid createdByUserId,
        string? title = null)
    {
        Type = type;
        CreatedByUserId = createdByUserId;
        Title = title;
    }
}