using Messenger.Application.Auditing;
using Messenger.Application.Users.MakeAdmin;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserAdminMaker : IUserAdminMaker
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public UserAdminMaker(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<MakeUserAdminResult> MakeAdminAsync(
        MakeUserAdminCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
     .FirstOrDefaultAsync(x => x.Id == command.TargetUserId, cancellationToken);

        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.MakeAdmin();

        await _auditLogWriter.WriteAsync(
            "UserMadeAdmin",
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

        return new MakeUserAdminResult(
            user.Id,
            user.DisplayName,
            user.UserName,
            user.IsAdmin);
    }
}