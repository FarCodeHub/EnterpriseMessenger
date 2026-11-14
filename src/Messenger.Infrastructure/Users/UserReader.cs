using Messenger.Application.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserReader : IUserReader
{
    private readonly MessengerDbContext _dbContext;

    public UserReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.DisplayName, x.UserName })
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(user?.DisplayName) ? user?.UserName : user.DisplayName;
    }

    public async Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new UserListItem(x.Id, x.DisplayName, x.UserName, x.IsActive, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
