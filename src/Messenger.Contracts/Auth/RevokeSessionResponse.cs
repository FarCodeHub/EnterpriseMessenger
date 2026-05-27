namespace Messenger.Contracts.Auth;

public sealed record RevokeSessionResponse(
    Guid UserId,
    string? DeviceId,
    int RevokedTokensCount,
    DateTime RevokedAtUtc
);