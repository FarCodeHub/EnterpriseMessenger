using Messenger.Contracts.Users;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Users.CreateUser;
using Messenger.Application.Users.RegisterDevice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Messenger.Application.Users.DeactivateUser;
using Messenger.Application.Users.ActivateUser;
using Messenger.Application.Users.MakeAdmin;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Messenger.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserCreator _userCreator;
    private readonly IUserDeviceRegistrar _userDeviceRegistrar;
    private readonly IUserDeactivator _userDeactivator;
    private readonly IUserActivator _userActivator;
    private readonly IUserAdminMaker _userAdminMaker;

    public UsersController(MessengerDbContext dbContext, IUserDeviceRegistrar userDeviceRegistrar, IUserCreator userCreator, IUserDeactivator userDeactivator, IUserActivator userActivator, IUserAdminMaker userAdminMaker)
    {
        _dbContext = dbContext;
        _userDeviceRegistrar = userDeviceRegistrar;
        _userCreator = userCreator;
        _userDeactivator = userDeactivator;
        _userActivator = userActivator;
        _userAdminMaker = userAdminMaker;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("UserId not found in token.");

        return Guid.Parse(userIdValue);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{userId:guid}/make-admin")]
    public async Task<IActionResult> MakeAdmin(
       Guid userId,
       CancellationToken cancellationToken)
    {
        var result = await _userAdminMaker.MakeAdminAsync(
          new MakeUserAdminCommand(
    GetCurrentUserId(),
    userId),
            cancellationToken);

        return Ok(result);
    }



    [Authorize(Roles = "Admin")]
    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> ActivateUser(
       Guid userId,
       CancellationToken cancellationToken)
    {
        var result = await _userActivator.ActivateAsync(
            new ActivateUserCommand(
        GetCurrentUserId(),
        userId),
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(
       Guid userId,
       CancellationToken cancellationToken)
    {
        var result = await _userDeactivator.DeactivateAsync(
            new DeactivateUserCommand(GetCurrentUserId(), userId),
            cancellationToken);

        return Ok(result);
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