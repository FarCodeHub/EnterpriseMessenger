using Messenger.Api.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class PresenceController : ControllerBase
{
    private readonly IConnectionTracker _connectionTracker;

    public PresenceController(IConnectionTracker connectionTracker)
    {
        _connectionTracker = connectionTracker;
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> IsUserOnline(Guid userId)
    {
        var connections = await _connectionTracker.GetConnectionsAsync(userId);

        return Ok(new
        {
            UserId = userId,
            IsOnline = connections.Count > 0,
            ConnectionCount = connections.Count
        });
    }

    [HttpGet("online-users")]
    public async Task<IActionResult> GetOnlineUsers()
    {
        var onlineUsers = await _connectionTracker.GetOnlineUsersAsync();

        return Ok(new
        {
            Count = onlineUsers.Count,
            Users = onlineUsers
        });
    }
}