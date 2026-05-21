namespace Messenger.Application.Auth.Refresh;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string DeviceId
);