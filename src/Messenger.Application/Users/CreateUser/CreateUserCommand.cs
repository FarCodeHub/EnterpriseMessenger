namespace Messenger.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string DisplayName,
    string UserName
);