using Messenger.Api.Auth;
using Messenger.Contracts.Auth;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Messenger.Domain.Users;

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


}