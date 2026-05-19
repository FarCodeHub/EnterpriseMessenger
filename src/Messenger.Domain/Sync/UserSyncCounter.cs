using Messenger.Domain.Common;

namespace Messenger.Domain.Sync;

public class UserSyncCounter : BaseEntity
{
    public Guid UserId { get; private set; }

    public long LastEventId { get; private set; }

    private UserSyncCounter()
    {
    }

    public UserSyncCounter(Guid userId)
    {
        UserId = userId;
        LastEventId = 0;
    }

    public long GetNextEventId()
    {
        LastEventId++;
        return LastEventId;
    }
}