namespace Messenger.Application.Auth.Login;

public sealed record LoginCommand(
    string UserName,
    string DeviceId
);