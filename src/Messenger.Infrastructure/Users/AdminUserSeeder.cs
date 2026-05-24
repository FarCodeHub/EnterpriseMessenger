using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Messenger.Infrastructure.Users;

public sealed class AdminUserSeeder
{
    private readonly MessengerDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AdminUserSeeder(
        MessengerDbContext dbContext,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var userName = _configuration["SeedAdmin:UserName"];
        var displayName = _configuration["SeedAdmin:DisplayName"];

        if (string.IsNullOrWhiteSpace(userName))
            return;

        var exists = await _dbContext.Users
            .AnyAsync(x => x.UserName == userName, cancellationToken);

        if (exists)
            return;

        var user = new User(displayName ?? "System Admin", userName);
        user.MakeAdmin();

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}