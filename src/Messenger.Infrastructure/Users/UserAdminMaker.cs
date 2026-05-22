using Messenger.Application.Users.MakeAdmin;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserAdminMaker : IUserAdminMaker
{
    private readonly MessengerDbContext _dbContext;

    public UserAdminMaker(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MakeUserAdminResult> MakeAdminAsync(
        MakeUserAdminCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == command.UserId, cancellationToken);

        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.MakeAdmin();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MakeUserAdminResult(
            user.Id,
            user.DisplayName,
            user.UserName,
            user.IsAdmin);
    }
}