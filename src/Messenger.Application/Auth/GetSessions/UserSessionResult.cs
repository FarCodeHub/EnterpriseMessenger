namespace Messenger.Application.Auth.GetSessions;

public sealed record UserSessionResult(
    string DeviceId,
    int ActiveRefreshTokensCount,
    DateTime LatestExpiresAtUtc,
    DateTime CreatedAtUtc
);