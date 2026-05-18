namespace Messenger.Contracts.Users;

public sealed record CreateUserRequest(
    string DisplayName,
    string UserName
);