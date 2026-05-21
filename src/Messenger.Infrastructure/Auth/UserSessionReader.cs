using Messenger.Application.Auth.GetSessions;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Auth;

public sealed class UserSessionReader : IUserSessionReader
{
    private readonly MessengerDbContext _dbContext;

    public UserSessionReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<UserSessionResult>> GetAsync(
        GetSessionsQuery query,
        CancellationToken cancellationToken)
    {
        return await _dbContext.RefreshTokens
            .Where(x =>
                x.UserId == query.UserId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > DateTime.UtcNow)
            .GroupBy(x => x.DeviceId)
            .Select(x => new UserSessionResult(
                x.Key,
                x.Count(),
                x.Max(t => t.ExpiresAtUtc),
                x.Min(t => t.CreatedAtUtc)))
            .OrderBy(x => x.DeviceId)
            .ToListAsync(cancellationToken);
    }
}