using Messenger.Application.Auth;
using Messenger.Application.Auth.Login;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Auth;

public sealed class UserLoginService : IUserLoginService
{
    private readonly MessengerDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public UserLoginService(
        MessengerDbContext dbContext,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthTokenResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == command.UserName, cancellationToken);

        if (user is null)
            throw new UnauthorizedAccessException("Invalid username.");

        var accessToken = _tokenService.CreateAccessToken(
            user.Id,
            user.UserName,
            user.DisplayName);

        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);

        _dbContext.RefreshTokens.Add(
            new RefreshToken(
                user.Id,
                refreshTokenHash,
                command.DeviceId,
                DateTime.UtcNow.AddDays(30)));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenResult(
            accessToken,
            refreshToken,
            "Bearer",
            8 * 60 * 60,
            new AuthUserResult(
                user.Id,
                user.DisplayName,
                user.UserName));
    }
}