

using Messenger.Application.Auth.Login;

namespace Messenger.Application.Auth.Refresh;

public interface IRefreshTokenService
{
    Task<AuthTokenResult> RefreshAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken);
}