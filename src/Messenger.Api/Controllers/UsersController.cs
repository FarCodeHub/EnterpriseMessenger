using Messenger.Contracts.Users;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;

    public UsersController(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var userNameExists = await _dbContext.Users
            .AnyAsync(x => x.UserName == request.UserName, cancellationToken);

        if (userNameExists)
            return Conflict("Username already exists.");

        var user = new User(request.DisplayName, request.UserName);

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            user.Id,
            user.DisplayName,
            user.UserName
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .OrderBy(x => x.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.UserName,
                x.IsActive,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost("{userId:guid}/devices")]
    public async Task<IActionResult> RegisterDevice(
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            return NotFound("User not found.");

        var existingDevice = await _dbContext.UserDevices
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.DeviceId == request.DeviceId,
                cancellationToken);

        if (existingDevice is not null)
        {
            return Ok(new
            {
                existingDevice.Id,
                existingDevice.UserId,
                existingDevice.DeviceId,
                existingDevice.DeviceName,
                existingDevice.LastSyncedEventId
            });
        }

        var device = new UserDevice(
            userId,
            request.DeviceId,
            request.DeviceName);

        _dbContext.UserDevices.Add(device);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            device.Id,
            device.UserId,
            device.DeviceId,
            device.DeviceName,
            device.LastSyncedEventId
        });
    }
}