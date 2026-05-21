using Messenger.Application.Auditing;
using Messenger.Application.Auth.RevokeSession;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Auth;

public sealed class SessionRevoker : ISessionRevoker
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public SessionRevoker(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
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
        await _auditLogWriter.WriteAsync(
    string.IsNullOrWhiteSpace(command.DeviceId) ? "LogoutAll" : "LogoutDevice",
    command.UserId,
    "User",
    command.UserId.ToString(),
    new
    {
        command.DeviceId,
        RevokedTokensCount = activeTokens.Count
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RevokeSessionResult(
            command.UserId,
            command.DeviceId,
            activeTokens.Count,
            DateTime.UtcNow);
    }
}