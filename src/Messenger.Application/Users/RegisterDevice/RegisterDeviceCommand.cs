namespace Messenger.Application.Users.RegisterDevice;

public sealed record RegisterDeviceCommand(
    Guid UserId,
    string DeviceId,
    string DeviceName
);