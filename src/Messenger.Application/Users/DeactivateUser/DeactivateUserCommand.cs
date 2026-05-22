namespace Messenger.Application.Users.DeactivateUser;

public sealed record DeactivateUserCommand(
    Guid ActorUserId,
    Guid TargetUserId
);