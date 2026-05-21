namespace Messenger.Contracts.Auth;

public sealed record RefreshTokenRequest(
    string RefreshToken,
    string DeviceId
);