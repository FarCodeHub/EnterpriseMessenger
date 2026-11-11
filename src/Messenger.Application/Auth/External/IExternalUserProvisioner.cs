namespace Messenger.Application.Auth.External;

/// <summary>
/// Maps a user signed in through the organization's identity provider (Keycloak) to a local messenger user,
/// creating or linking the local user on first sign-in.
/// </summary>
public interface IExternalUserProvisioner
{
    Task<ExternalUserResult> ProvisionAsync(
        ExternalUserCommand command,
        CancellationToken cancellationToken);
}

public sealed record ExternalUserCommand(
    string Subject,
    string UserName,
    string? DisplayName);

public sealed record ExternalUserResult(
    Guid UserId,
    string UserName,
    string DisplayName,
    bool IsAdmin,
    bool IsActive);
