using Messenger.Application.Users.CreateUser;
using Messenger.Domain.Sync;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserCreator : IUserCreator
{
    private readonly MessengerDbContext _dbContext;

    public UserCreator(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateUserResult> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userNameExists = await _dbContext.Users
            .AnyAsync(x => x.UserName == command.UserName, cancellationToken);

        if (userNameExists)
            throw new InvalidOperationException("Username already exists.");

        var user = new User(command.DisplayName, command.UserName);

        _dbContext.Users.Add(user);
        _dbContext.UserSyncCounters.Add(new UserSyncCounter(user.Id));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateUserResult(
            user.Id,
            user.DisplayName,
            user.UserName);
    }
}