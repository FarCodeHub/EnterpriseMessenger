namespace Messenger.Application.Auth.GetSessions;

public interface IUserSessionReader
{
    Task<IReadOnlyCollection<UserSessionResult>> GetAsync(
        GetSessionsQuery query,
        CancellationToken cancellationToken);
}