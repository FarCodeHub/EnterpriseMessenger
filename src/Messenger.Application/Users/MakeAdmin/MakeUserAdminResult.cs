namespace Messenger.Application.Users.MakeAdmin;

public sealed record MakeUserAdminResult(
    Guid Id,
    string DisplayName,
    string UserName,
    bool IsAdmin
);