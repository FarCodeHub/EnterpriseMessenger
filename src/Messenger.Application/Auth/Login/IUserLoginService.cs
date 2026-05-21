namespace Messenger.Application.Auth.Login;

public interface IUserLoginService
{
    Task<AuthTokenResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken);
}