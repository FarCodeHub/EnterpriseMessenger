using Messenger.Api.Auth;
using Messenger.Contracts.Auth;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Messenger.Domain.Users;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        MessengerDbContext dbContext,
        JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken);

        if (user is null)
            return Unauthorized("Invalid username.");

        var token = _jwtTokenService.CreateToken(
            user.Id,
            user.UserName,
            user.DisplayName);



        var refreshToken = JwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = JwtTokenService.HashRefreshToken(refreshToken);

        var refreshTokenEntity = new RefreshToken(
            user.Id,
            refreshTokenHash,
            request.DeviceId,
            DateTime.UtcNow.AddDays(30));

        _dbContext.RefreshTokens.Add(refreshTokenEntity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresInSeconds = 8 * 60 * 60,
            User = new
            {
                user.Id,
                user.DisplayName,
                user.UserName
            }
        });
    }


    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name;
        var displayName = User.FindFirstValue("display_name");

        return Ok(new
        {
            UserId = userId,
            UserName = userName,
            DisplayName = displayName
        });
    }

[HttpPost("refresh")]
public async Task<IActionResult> Refresh(
    RefreshTokenRequest request,
    CancellationToken cancellationToken)
{
    var refreshTokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);

    var refreshToken = await _dbContext.RefreshTokens
        .FirstOrDefaultAsync(x =>
            x.TokenHash == refreshTokenHash &&
            x.DeviceId == request.DeviceId,
            cancellationToken);

    if (refreshToken is null)
        return Unauthorized("Invalid refresh token.");

    if (refreshToken.IsRevoked)
        return Unauthorized("Refresh token is revoked.");

    if (refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
        return Unauthorized("Refresh token is expired.");

    var user = await _dbContext.Users
        .FirstOrDefaultAsync(x => x.Id == refreshToken.UserId, cancellationToken);

    if (user is null || !user.IsActive)
        return Unauthorized("User is not active.");

    refreshToken.Revoke();

    var newRefreshToken = JwtTokenService.GenerateRefreshToken();
    var newRefreshTokenHash = JwtTokenService.HashRefreshToken(newRefreshToken);

    _dbContext.RefreshTokens.Add(
        new RefreshToken(
            user.Id,
            newRefreshTokenHash,
            request.DeviceId,
            DateTime.UtcNow.AddDays(30)));

    var newAccessToken = _jwtTokenService.CreateToken(
        user.Id,
        user.UserName,
        user.DisplayName);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        AccessToken = newAccessToken,
        RefreshToken = newRefreshToken,
        TokenType = "Bearer",
        ExpiresInSeconds = 8 * 60 * 60,
        User = new
        {
            user.Id,
            user.DisplayName,
            user.UserName
        }
    });
}

[Authorize]
[HttpPost("logout")]
public async Task<IActionResult> Logout(
    LogoutRequest request,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var activeRefreshTokens = await _dbContext.RefreshTokens
        .Where(x =>
            x.UserId == currentUserId &&
            x.DeviceId == request.DeviceId &&
            x.RevokedAtUtc == null)
        .ToListAsync(cancellationToken);

    foreach (var refreshToken in activeRefreshTokens)
    {
        refreshToken.Revoke();
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        UserId = currentUserId,
        request.DeviceId,
        RevokedTokensCount = activeRefreshTokens.Count,
        LoggedOutAtUtc = DateTime.UtcNow
    });
}

private Guid GetCurrentUserId()
{
    var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(userIdValue))
        throw new UnauthorizedAccessException("UserId not found in token.");

    return Guid.Parse(userIdValue);
}


[Authorize]
[HttpPost("logout-all")]
public async Task<IActionResult> LogoutAll(
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var activeRefreshTokens = await _dbContext.RefreshTokens
        .Where(x =>
            x.UserId == currentUserId &&
            x.RevokedAtUtc == null)
        .ToListAsync(cancellationToken);

    foreach (var refreshToken in activeRefreshTokens)
    {
        refreshToken.Revoke();
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        UserId = currentUserId,
        RevokedTokensCount = activeRefreshTokens.Count,
        LoggedOutAtUtc = DateTime.UtcNow
    });
}


[Authorize]
[HttpGet("sessions")]
public async Task<IActionResult> GetSessions(
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var sessions = await _dbContext.RefreshTokens
        .Where(x =>
            x.UserId == currentUserId &&
            x.RevokedAtUtc == null &&
            x.ExpiresAtUtc > DateTime.UtcNow)
        .GroupBy(x => x.DeviceId)
        .Select(x => new
        {
            DeviceId = x.Key,
            ActiveRefreshTokensCount = x.Count(),
            LatestExpiresAtUtc = x.Max(t => t.ExpiresAtUtc),
            CreatedAtUtc = x.Min(t => t.CreatedAtUtc)
        })
        .OrderBy(x => x.DeviceId)
        .ToListAsync(cancellationToken);

    return Ok(sessions);
}

[Authorize]
[HttpDelete("sessions/{deviceId}")]
public async Task<IActionResult> RevokeSession(
    string deviceId,
    CancellationToken cancellationToken)
{
    var currentUserId = GetCurrentUserId();

    var activeRefreshTokens = await _dbContext.RefreshTokens
        .Where(x =>
            x.UserId == currentUserId &&
            x.DeviceId == deviceId &&
            x.RevokedAtUtc == null)
        .ToListAsync(cancellationToken);

    foreach (var refreshToken in activeRefreshTokens)
    {
        refreshToken.Revoke();
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        UserId = currentUserId,
        DeviceId = deviceId,
        RevokedTokensCount = activeRefreshTokens.Count,
        RevokedAtUtc = DateTime.UtcNow
    });
}

}