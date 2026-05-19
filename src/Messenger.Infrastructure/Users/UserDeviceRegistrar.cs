using Messenger.Application.Users.RegisterDevice;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Users;

public sealed class UserDeviceRegistrar : IUserDeviceRegistrar
{
    private readonly MessengerDbContext _dbContext;

    public UserDeviceRegistrar(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegisterDeviceResult> RegisterAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users
            .AnyAsync(x => x.Id == command.UserId, cancellationToken);

        if (!userExists)
            throw new InvalidOperationException("User not found.");

        var existingDevice = await _dbContext.UserDevices
            .FirstOrDefaultAsync(
                x => x.UserId == command.UserId &&
                     x.DeviceId == command.DeviceId,
                cancellationToken);

        if (existingDevice is not null)
        {
            return new RegisterDeviceResult(
                existingDevice.Id,
                existingDevice.UserId,
                existingDevice.DeviceId,
                existingDevice.DeviceName,
                existingDevice.LastSyncedEventId);
        }

        var device = new UserDevice(
            command.UserId,
            command.DeviceId,
            command.DeviceName);

        _dbContext.UserDevices.Add(device);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterDeviceResult(
            device.Id,
            device.UserId,
            device.DeviceId,
            device.DeviceName,
            device.LastSyncedEventId);
    }
}