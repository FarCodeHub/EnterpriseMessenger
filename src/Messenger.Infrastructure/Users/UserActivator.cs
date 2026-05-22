using Messenger.Application.Users.ActivateUser;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserActivator : IUserActivator
{
    private readonly MessengerDbContext _dbContext;

    public UserActivator(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ActivateUserResult(
            user.Id,
            user.DisplayName,
            user.UserName,
            user.IsActive);
    }
}