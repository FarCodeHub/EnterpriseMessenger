using Messenger.Domain.Common;

namespace Messenger.Domain.Users;

public class UserDevice : BaseEntity
{
    public Guid UserId { get; private set; }

    public string DeviceId { get; private set; }

    public string DeviceName { get; private set; }

    public long LastSyncedEventId { get; private set; }

    private UserDevice()
    {
    }

    public UserDevice(
        Guid userId,
        string deviceId,
        string deviceName)
    {
        UserId = userId;
        DeviceId = deviceId;
        DeviceName = deviceName;
    }

    public void UpdateLastSyncedEventId(long eventId)
    {
        LastSyncedEventId = eventId;
    }
}