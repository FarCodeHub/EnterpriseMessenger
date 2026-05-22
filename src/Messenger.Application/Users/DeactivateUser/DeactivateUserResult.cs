namespace Messenger.Application.Users.DeactivateUser;

public sealed record DeactivateUserResult(
    Guid Id,
    string DisplayName,
    string UserName,
    bool IsActive,
    int RevokedTokensCount
);