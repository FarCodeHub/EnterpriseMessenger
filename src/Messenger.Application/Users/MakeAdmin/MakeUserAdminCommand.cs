namespace Messenger.Application.Users.MakeAdmin;

public sealed record MakeUserAdminCommand(
    Guid ActorUserId,
    Guid TargetUserId
);