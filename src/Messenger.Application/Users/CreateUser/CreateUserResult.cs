namespace Messenger.Application.Users.CreateUser;

public sealed record CreateUserResult(
    Guid Id,
    string DisplayName,
    string UserName
);