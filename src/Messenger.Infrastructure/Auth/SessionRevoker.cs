using Messenger.Application.Auth.RevokeSession;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Auth;

public sealed class SessionRevoker : ISessionRevoker
{
    private readonly MessengerDbContext _dbContext;

    public SessionRevoker(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RevokeSessionResult> RevokeAsync(
        RevokeSessionCommand command,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.RefreshTokens
            .Where(x =>
                x.UserId == command.UserId &&
                x.RevokedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(command.DeviceId))
        {
            query = query.Where(x => x.DeviceId == command.DeviceId);
        }

        var activeTokens = await query.ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RevokeSessionResult(
            command.UserId,
            command.DeviceId,
            activeTokens.Count,
            DateTime.UtcNow);
    }
}