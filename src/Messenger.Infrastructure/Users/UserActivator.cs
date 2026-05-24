using Messenger.Application.Auditing;
using Messenger.Application.Users.ActivateUser;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserActivator : IUserActivator
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public UserActivator(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<ActivateUserResult> ActivateAsync(
        ActivateUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
     .FirstOrDefaultAsync(x => x.Id == command.TargetUserId, cancellationToken);

        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.Activate();

        await _auditLogWriter.WriteAsync(
            "UserActivated",
            command.ActorUserId,
            "User",
            user.Id.ToString(),
            new
            {
                TargetUserId = user.Id,
                user.UserName
            },
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
return new ActivateUserResult(
    user.Id,
    user.DisplayName,
    user.UserName,
    user.IsActive,
    user.IsAdmin);
    }
}