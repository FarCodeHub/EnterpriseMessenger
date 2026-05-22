namespace Messenger.Application.Users.ActivateUser;

public interface IUserActivator
{
    Task<ActivateUserResult> ActivateAsync(
        ActivateUserCommand command,
        CancellationToken cancellationToken);
}