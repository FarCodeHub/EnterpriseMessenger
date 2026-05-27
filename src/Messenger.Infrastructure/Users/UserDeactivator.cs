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


          if (command.ActorUserId == command.TargetUserId)
    throw new InvalidOperationException("You cannot deactivate yourself.");

        var user = await _dbContext.Users
      .FirstOrDefaultAsync(x => x.Id == command.TargetUserId, cancellationToken);

 if (user is null)
            throw new InvalidOperationException("User not found.");

    if (user.IsAdmin)
{
    var activeAdminsCount = await _dbContext.Users
        .CountAsync(x => x.IsAdmin && x.IsActive, cancellationToken);

    if (activeAdminsCount <= 1)
        throw new InvalidOperationException("Cannot deactivate the last active admin.");
}

       

        user.Deactivate();

        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(x =>
                x.UserId == command.TargetUserId &&
                x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeRefreshTokens)
        {
            token.Revoke();
        }


        await _auditLogWriter.WriteAsync(
            "UserDeactivated",
            command.ActorUserId,
            "User",
            user.Id.ToString(),
            new
            {
                TargetUserId = user.Id,
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
    user.IsAdmin,
    activeRefreshTokens.Count);
    }
}