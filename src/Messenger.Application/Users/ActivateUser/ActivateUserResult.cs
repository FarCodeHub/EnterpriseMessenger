public sealed record ActivateUserResult(
    Guid Id,
    string DisplayName,
    string UserName,
    bool IsActive,
    bool IsAdmin
);