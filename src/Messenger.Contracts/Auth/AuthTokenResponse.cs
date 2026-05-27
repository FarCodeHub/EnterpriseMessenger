namespace Messenger.Contracts.Auth;

public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    AuthUserResponse User
);

public sealed record AuthUserResponse(
    Guid Id,
    string DisplayName,
    string UserName
);