using Messenger.Application.Users;
using Messenger.Contracts.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger.Application.Users.CreateUser;
using Messenger.Application.Users.RegisterDevice;
using Microsoft.AspNetCore.Authorization;
using Messenger.Application.Users.DeactivateUser;
using Messenger.Application.Users.ActivateUser;
using Messenger.Application.Users.MakeAdmin;
using System.Security.Claims;
using Messenger.Api.Common;

namespace Messenger.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserReader _userReader;
    private readonly IUserCreator _userCreator;
    private readonly IUserDeviceRegistrar _userDeviceRegistrar;
    private readonly IUserDeactivator _userDeactivator;
    private readonly IUserActivator _userActivator;
    private readonly IUserAdminMaker _userAdminMaker;

    public UsersController(IUserReader userReader, IUserDeviceRegistrar userDeviceRegistrar, IUserCreator userCreator, IUserDeactivator userDeactivator, IUserActivator userActivator, IUserAdminMaker userAdminMaker)
    {
        _userReader = userReader;
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





    [ProducesResponseType(typeof(UserAdminActionResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
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

return Ok(new UserAdminActionResponse(
    result.Id,
    result.DisplayName,
    result.UserName,
    IsActive: true,
    result.IsAdmin));
    }



    [ProducesResponseType(typeof(UserAdminActionResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
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

return Ok(new UserAdminActionResponse(
    result.Id,
    result.DisplayName,
    result.UserName,
    result.IsActive,
    result.IsAdmin));
    }

    [ProducesResponseType(typeof(UserAdminActionResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin")]
    [HttpPost("{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(
       Guid userId,
       CancellationToken cancellationToken)
    {
        var result = await _userDeactivator.DeactivateAsync(
            new DeactivateUserCommand(GetCurrentUserId(), userId),
            cancellationToken);

return Ok(new UserAdminActionResponse(
    result.Id,
    result.DisplayName,
    result.UserName,
    result.IsActive,
    result.IsAdmin,
    result.RevokedTokensCount));
    }



    [Authorize(Roles = "Admin")]
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
        var users = await _userReader.GetAllAsync(cancellationToken);

        return Ok(users);
    }




    [HttpPost("{userId:guid}/devices")]
    public async Task<IActionResult> RegisterDevice(
       Guid userId,
       RegisterDeviceRequest request,
       CancellationToken cancellationToken)
    {
        // Devices can only be registered for yourself, unless you are an admin.
        if (userId != GetCurrentUserId() && !User.IsInRole("Admin"))
            return Forbid();

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