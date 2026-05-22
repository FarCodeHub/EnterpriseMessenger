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
            .FirstOrDefaultAsync(x => x.Id == command.UserId, cancellationToken);

        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.Activate();

        await _auditLogWriter.WriteAsync(
    "UserActivated",
    command.UserId,
    "User",
    user.Id.ToString(),
    new
    {
        user.UserName
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ActivateUserResult(
            user.Id,
            user.DisplayName,
            user.UserName,
            user.IsActive);
    }
}