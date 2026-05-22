namespace Messenger.Application.Users.DeactivateUser;

public interface IUserDeactivator
{
    Task<DeactivateUserResult> DeactivateAsync(
        DeactivateUserCommand command,
        CancellationToken cancellationToken);
}