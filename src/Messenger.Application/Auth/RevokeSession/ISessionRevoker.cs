namespace Messenger.Application.Auth.RevokeSession;

public interface ISessionRevoker
{
    Task<RevokeSessionResult> RevokeAsync(
        RevokeSessionCommand command,
        CancellationToken cancellationToken);
}