using Messenger.Contracts.Users;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Users.CreateUser;
using Messenger.Application.Users.RegisterDevice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Messenger.Api.Controllers;
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserCreator _userCreator;
    private readonly IUserDeviceRegistrar _userDeviceRegistrar;

    public UsersController(MessengerDbContext dbContext, IUserDeviceRegistrar userDeviceRegistrar, IUserCreator userCreator)
    {
        _dbContext = dbContext;
        _userDeviceRegistrar = userDeviceRegistrar;
        _userCreator = userCreator;
    }


[Authorize(Roles = "Admin")]
[HttpPost("{userId:guid}/make-admin")]
public async Task<IActionResult> MakeAdmin(
    Guid userId,
    CancellationToken cancellationToken)
{
    var user = await _dbContext.Users
        .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    if (user is null)
        return NotFound("User not found.");

    user.MakeAdmin();

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Ok(new
    {
        user.Id,
        user.DisplayName,
        user.UserName,
        user.IsAdmin
    });
}

  [HttpPost]
public async Task<IActionResult> CreateUser(
    CreateUserRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await _userCreator.CreateAsync(
            new CreateUserCommand(
                request.DisplayName,
                request.UserName),
            cancellationToken);

        return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Conflict(ex.Message);
    }
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
    try
    {
        var result = await _userDeviceRegistrar.RegisterAsync(
            new RegisterDeviceCommand(
                userId,
                request.DeviceId,
                request.DeviceName),
            cancellationToken);

        return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(ex.Message);
    }
}


}