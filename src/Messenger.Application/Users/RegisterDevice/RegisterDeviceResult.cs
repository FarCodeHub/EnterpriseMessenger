namespace Messenger.Application.Users.RegisterDevice;

public sealed record RegisterDeviceResult(
    Guid Id,
    Guid UserId,
    string DeviceId,
    string DeviceName,
    long LastSyncedEventId
);