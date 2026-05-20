using Messenger.Api.Auth;
using Messenger.Contracts.Auth;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        return Ok(new
        {
            AccessToken = token,
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