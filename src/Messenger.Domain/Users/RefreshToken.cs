using Messenger.Domain.Common;

namespace Messenger.Domain.Users;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public string DeviceId { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        string deviceId,
        DateTime expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        DeviceId = deviceId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Revoke()
    {
        RevokedAtUtc = DateTime.UtcNow;
    }
}