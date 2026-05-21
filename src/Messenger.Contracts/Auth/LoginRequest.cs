namespace Messenger.Contracts.Auth;

public sealed record LoginRequest(
    string UserName,
    string DeviceId
);