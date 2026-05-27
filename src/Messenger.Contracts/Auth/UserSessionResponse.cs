namespace Messenger.Contracts.Auth;

public sealed record UserSessionResponse(
    string DeviceId,
    int ActiveRefreshTokensCount,
    DateTime LatestExpiresAtUtc,
    DateTime CreatedAtUtc
);