namespace Messenger.Contracts.Users;

public sealed record UserAdminActionResponse(
    Guid Id,
    string DisplayName,
    string UserName,
    bool IsActive,
    bool IsAdmin,
    int? RevokedTokensCount = null
);