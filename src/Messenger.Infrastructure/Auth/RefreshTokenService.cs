using Messenger.Application.Auth;
using Messenger.Application.Auth.Login;
using Messenger.Application.Auth.Refresh;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly MessengerDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public RefreshTokenService(
        MessengerDbContext dbContext,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthTokenResult> RefreshAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var refreshTokenHash = _tokenService.HashRefreshToken(command.RefreshToken);

        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x =>
                x.TokenHash == refreshTokenHash &&
                x.DeviceId == command.DeviceId,
                cancellationToken);

        if (refreshToken is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        if (refreshToken.IsRevoked)
            throw new UnauthorizedAccessException("Refresh token is revoked.");

        if (refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token is expired.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == refreshToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User is not active.");

        refreshToken.Revoke();

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);

        _dbContext.RefreshTokens.Add(
            new RefreshToken(
                user.Id,
                newRefreshTokenHash,
                command.DeviceId,
                DateTime.UtcNow.AddDays(30)));

        var newAccessToken = _tokenService.CreateAccessToken(
            user.Id,
            user.UserName,
            user.DisplayName);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenResult(
            newAccessToken,
            newRefreshToken,
            "Bearer",
            8 * 60 * 60,
            new AuthUserResult(
                user.Id,
                user.DisplayName,
                user.UserName));
    }
}