namespace Messenger.Application.Users.CreateUser;

public interface IUserCreator
{
    Task<CreateUserResult> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken);
}