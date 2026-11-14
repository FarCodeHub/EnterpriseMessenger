namespace Messenger.Application.Users;

public interface IUserReader
{
    /// <summary>The name shown for the user: display name, or user name when it has none. Null for unknown users.</summary>
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>All users, ordered by display name.</summary>
    Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed record UserListItem(
    Guid Id,
    string DisplayName,
    string UserName,
    bool IsActive,
    DateTime CreatedAtUtc);
