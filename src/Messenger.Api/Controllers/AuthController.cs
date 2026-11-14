using Messenger.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Messenger.Application.Auth.Login;
using Messenger.Application.Auth.Refresh;
using Messenger.Application.Auth.RevokeSession;
using Messenger.Application.Auth.GetSessions;
using Messenger.Api.Common;
using Messenger.Api.Auth;
namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserLoginService _userLoginService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUserSessionReader _userSessionReader;
    private readonly ISessionRevoker _sessionRevoker;
    private readonly AuthModeSettings _authMode;

    public AuthController(
        IUserLoginService userLoginService,
        IRefreshTokenService refreshTokenService,
        ISessionRevoker sessionRevoker,
        IUserSessionReader userSessionReader,
        AuthModeSettings authMode)
    {
        _authMode = authMode;
        _userLoginService = userLoginService;
        _refreshTokenService = refreshTokenService;
        _sessionRevoker = sessionRevoker;
        _userSessionReader = userSessionReader;
    }




    [ProducesResponseType(typeof(AuthUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
    LoginRequest request,
    CancellationToken cancellationToken)
    {
        if (!_authMode.LocalLoginEnabled)
            return LocalLoginDisabled();

        var result = await _userLoginService.LoginAsync(
            new LoginCommand(request.UserName, request.DeviceId),
            cancellationToken);

        return Ok(new AuthTokenResponse(
     result.AccessToken,
     result.RefreshToken,
     result.TokenType,
     result.ExpiresInSeconds,
     new AuthUserResponse(
         result.User.Id,
         result.User.DisplayName,
         result.User.UserName)));

    }

    /// <summary>
    /// Tells the web app how to sign in: through Keycloak, the local username login (development), or both.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var keycloak = _authMode.Keycloak;

        return Ok(new
        {
            LocalLoginEnabled = _authMode.LocalLoginEnabled,
            Keycloak = keycloak.IsConfigured
                ? new
                {
                    Authority = keycloak.Authority!.TrimEnd('/'),
                    keycloak.ClientId
                }
                : null
        });
    }

    private ObjectResult LocalLoginDisabled() =>
        StatusCode(
            StatusCodes.Status404NotFound,
            new ApiErrorResponse(
                "local_login_disabled",
                "Sign in through the organization account.",
                StatusCodes.Status404NotFound,
                DateTime.UtcNow));

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



/// <summary>
/// Refreshes JWT access token.
/// </summary>
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
    RefreshTokenRequest request,
    CancellationToken cancellationToken)
    {
        if (!_authMode.LocalLoginEnabled)
            return LocalLoginDisabled();

        var result = await _refreshTokenService.RefreshAsync(
            new RefreshTokenCommand(
                request.RefreshToken,
                request.DeviceId),
            cancellationToken);

        return Ok(new AuthTokenResponse(
    result.AccessToken,
    result.RefreshToken,
    result.TokenType,
    result.ExpiresInSeconds,
    new AuthUserResponse(
        result.User.Id,
        result.User.DisplayName,
        result.User.UserName)));

    }


    [ProducesResponseType(typeof(RevokeSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
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

        return Ok(new RevokeSessionResponse(
          result.UserId,
          result.DeviceId,
          result.RevokedTokensCount,
          result.RevokedAtUtc));
    }



    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("UserId not found in token.");

        return Guid.Parse(userIdValue);
    }



    [ProducesResponseType(typeof(RevokeSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
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

        return Ok(new RevokeSessionResponse(
           result.UserId,
           result.DeviceId,
           result.RevokedTokensCount,
           result.RevokedAtUtc));
    }






    [ProducesResponseType(typeof(UserSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
    CancellationToken cancellationToken)
    {
        var result = await _userSessionReader.GetAsync(
            new GetSessionsQuery(GetCurrentUserId()),
            cancellationToken);

        return Ok(result.Select(x => new UserSessionResponse(
          x.DeviceId,
          x.ActiveRefreshTokensCount,
          x.LatestExpiresAtUtc,
          x.CreatedAtUtc)));
    }



    [ProducesResponseType(typeof(RevokeSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
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

        return Ok(new RevokeSessionResponse(
          result.UserId,
          result.DeviceId,
          result.RevokedTokensCount,
          result.RevokedAtUtc));
    }
}