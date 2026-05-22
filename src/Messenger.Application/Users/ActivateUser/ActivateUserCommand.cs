namespace Messenger.Application.Users.ActivateUser;

public sealed record ActivateUserCommand(
    Guid UserId
);