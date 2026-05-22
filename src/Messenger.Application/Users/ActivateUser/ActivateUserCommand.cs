namespace Messenger.Application.Users.ActivateUser;

public sealed record ActivateUserCommand(
    Guid ActorUserId,
    Guid TargetUserId
);