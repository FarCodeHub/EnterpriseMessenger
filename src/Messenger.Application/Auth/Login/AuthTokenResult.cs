namespace Messenger.Application.Auth.Login;

public sealed record AuthTokenResult(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    AuthUserResult User
);

public sealed record AuthUserResult(
    Guid Id,
    string DisplayName,
    string UserName
);