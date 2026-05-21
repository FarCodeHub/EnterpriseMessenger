namespace Messenger.Application.Auth.RevokeSession;

public sealed record RevokeSessionCommand(
    Guid UserId,
    string? DeviceId
);