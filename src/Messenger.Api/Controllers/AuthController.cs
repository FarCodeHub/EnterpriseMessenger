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
using Messenger.Application.Auth.Login;
using Messenger.Application.Auth.Refresh;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController : ControllerBase
{
private readonly MessengerDbContext _dbContext;
private readonly IUserLoginService _userLoginService;
private readonly IRefreshTokenService _refreshTokenService;

public AuthController(
    MessengerDbContext dbContext,
    IUserLoginService userLoginService,
    IRefreshTokenService refreshTokenService)
{
    _dbContext = dbContext;
    _userLoginService = userLoginService;
    _refreshTokenService = refreshTokenService;
}
 [HttpPost("login")]
public async Task<IActionResult> Login(
    LoginRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await _userLoginService.LoginAsync(
            new LoginCommand(request.UserName, request.DeviceId),
            cancellationToken);

        return Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Unauthorized(ex.Message);
    }
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
    try
    {
        var result = await _refreshTokenService.RefreshAsync(
            new RefreshTokenCommand(
                request.RefreshToken,
                request.DeviceId),
            cancellationToken);

        return Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Unauthorized(ex.Message);
    }
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