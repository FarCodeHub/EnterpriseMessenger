namespace Messenger.Application.Auth;

public interface ITokenService
{
    string CreateAccessToken(Guid userId, string userName, string displayName);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}