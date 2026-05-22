using Messenger.Contracts.Auth;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Messenger.Application.Auth.Login;
using Messenger.Application.Auth.Refresh;
using Messenger.Application.Auth.RevokeSession;
using Messenger.Application.Auth.GetSessions;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController : ControllerBase
{
private readonly MessengerDbContext _dbContext;
private readonly IUserLoginService _userLoginService;
private readonly IRefreshTokenService _refreshTokenService;
private readonly IUserSessionReader _userSessionReader;
private readonly ISessionRevoker _sessionRevoker;

    public AuthController(
        MessengerDbContext dbContext,
        IUserLoginService userLoginService,
        IRefreshTokenService refreshTokenService,
        ISessionRevoker sessionRevoker,
        IUserSessionReader userSessionReader)
    {
        _dbContext = dbContext;
        _userLoginService = userLoginService;
        _refreshTokenService = refreshTokenService;
        _sessionRevoker = sessionRevoker;
        _userSessionReader = userSessionReader;
    }
    [HttpPost("login")]
public async Task<IActionResult> Login(
    LoginRequest request,
    CancellationToken cancellationToken)
{
    
        var result = await _userLoginService.LoginAsync(
            new LoginCommand(request.UserName, request.DeviceId),
            cancellationToken);

        return Ok(result);
    
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
    
        var result = await _refreshTokenService.RefreshAsync(
            new RefreshTokenCommand(
                request.RefreshToken,
                request.DeviceId),
            cancellationToken);

        return Ok(result);
   
}



[Authorize]
[HttpPost("logout")]
public async Task<IActionResult> Logout(
    LogoutRequest request,
    CancellationToken cancellationToken)
{
    var result = await _sessionRevoker.RevokeAsync(
        new RevokeSessionCommand(
            GetCurrentUserId(),
            request.DeviceId),
        cancellationToken);

    return Ok(result);
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
    var result = await _sessionRevoker.RevokeAsync(
        new RevokeSessionCommand(
            GetCurrentUserId(),
            DeviceId: null),
        cancellationToken);

    return Ok(result);
}
 

[Authorize]
[HttpGet("sessions")]
public async Task<IActionResult> GetSessions(
    CancellationToken cancellationToken)
{
    var result = await _userSessionReader.GetAsync(
        new GetSessionsQuery(GetCurrentUserId()),
        cancellationToken);

    return Ok(result);
}


 [Authorize]
[HttpDelete("sessions/{deviceId}")]
public async Task<IActionResult> RevokeSession(
    string deviceId,
    CancellationToken cancellationToken)
{
    var result = await _sessionRevoker.RevokeAsync(
        new RevokeSessionCommand(
            GetCurrentUserId(),
            deviceId),
        cancellationToken);

    return Ok(result);
}
}