namespace Messenger.Application.Users.RegisterDevice;

public interface IUserDeviceRegistrar
{
    Task<RegisterDeviceResult> RegisterAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken);
}