using Messenger.Application.Auditing;
using Messenger.Application.Auth.External;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Auth;

public sealed class ExternalUserProvisioner : IExternalUserProvisioner
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public ExternalUserProvisioner(
        MessengerDbContext dbContext,
        IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<ExternalUserResult> ProvisionAsync(
        ExternalUserCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Subject))
            throw new InvalidOperationException("Identity provider subject is missing.");

        if (string.IsNullOrWhiteSpace(command.UserName))
            throw new InvalidOperationException("Identity provider user name is missing.");

        var user = await FindOrCreateAsync(command, cancellationToken);

        return new ExternalUserResult(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.IsAdmin,
            user.IsActive);
    }

    private async Task<User> FindOrCreateAsync(
        ExternalUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.ExternalSubject == command.Subject, cancellationToken);

        if (user is not null)
        {
            if (!string.IsNullOrWhiteSpace(command.DisplayName) &&
                user.DisplayName != command.DisplayName)
            {
                user.UpdateDisplayName(command.DisplayName);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return user;
        }

        var normalizedUserName = command.UserName.Trim().ToLowerInvariant();

        // First sign-in: link an existing local account with the same user name so its
        // conversations and messages are kept, otherwise create a new account.
        user = await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.ExternalSubject == null && x.UserName.ToLower() == normalizedUserName,
                cancellationToken);

        var isNewUser = user is null;

        if (user is null)
        {
            user = new User(
                string.IsNullOrWhiteSpace(command.DisplayName) ? command.UserName : command.DisplayName,
                command.UserName.Trim());

            _dbContext.Users.Add(user);
        }
        else if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            user.UpdateDisplayName(command.DisplayName);
        }

        user.LinkExternalSubject(command.Subject);

        await _auditLogWriter.WriteAsync(
            isNewUser ? "ExternalUserCreated" : "ExternalUserLinked",
            user.Id,
            "User",
            user.Id.ToString(),
            new { command.Subject, command.UserName },
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request provisioned the same user concurrently; use the stored one.
            _dbContext.ChangeTracker.Clear();

            return await _dbContext.Users
                .FirstAsync(x => x.ExternalSubject == command.Subject, cancellationToken);
        }

        return user;
    }
}
