namespace Messenger.Contracts.Auth;

public sealed record LogoutRequest(
    string DeviceId
);