namespace Messenger.Application.Auth.RevokeSession;

public sealed record RevokeSessionResult(
    Guid UserId,
    string? DeviceId,
    int RevokedTokensCount,
    DateTime RevokedAtUtc
);