namespace Messenger.Contracts.Users;

public sealed record RegisterDeviceRequest(
    string DeviceId,
    string DeviceName
);