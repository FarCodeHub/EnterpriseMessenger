using Messenger.Api.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class PresenceController : ControllerBase
{
    private readonly InMemoryConnectionTracker _connectionTracker;

    public PresenceController(InMemoryConnectionTracker connectionTracker)
    {
        _connectionTracker = connectionTracker;
    }

    [HttpGet("users/{userId:guid}")]
    public IActionResult IsUserOnline(Guid userId)
    {
        var connections = _connectionTracker.GetConnections(userId);

        return Ok(new
        {
            UserId = userId,
            IsOnline = connections.Count > 0,
            ConnectionCount = connections.Count
        });
    }

    [HttpGet("online-users")]
    public IActionResult GetOnlineUsers()
    {
        var onlineUsers = _connectionTracker.GetOnlineUsers();

        return Ok(new
        {
            Count = onlineUsers.Count,
            Users = onlineUsers
        });
    }
}