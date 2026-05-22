using Messenger.Application.Auditing;
using Messenger.Application.Users.DeactivateUser;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserDeactivator : IUserDeactivator
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public UserDeactivator(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<DeactivateUserResult> DeactivateAsync(
        DeactivateUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == command.UserId, cancellationToken);

        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.Deactivate();

        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(x =>
                x.UserId == command.UserId &&
                x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeRefreshTokens)
        {
            token.Revoke();
        }


        await _auditLogWriter.WriteAsync(
    "UserDeactivated",
    command.UserId,
    "User",
    user.Id.ToString(),
    new
    {
        user.UserName,
        RevokedTokensCount = activeRefreshTokens.Count
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeactivateUserResult(
            user.Id,
            user.DisplayName,
            user.UserName,
            user.IsActive,
            activeRefreshTokens.Count);
    }
}