using System.Security.Cryptography;
using System.Text;

namespace Messenger.Api.Auth;

/// <summary>
/// Validates the JWT signing key at startup so the API never runs with a weak or publicly known key.
/// </summary>
public static class JwtSecretKeyResolver
{
    private const int MinimumKeyBytes = 32;

    // Keys that have been committed to the repository at some point. They are public and must never sign real tokens.
    private static readonly string[] KnownPublicKeys =
    [
        "THIS_IS_A_DEVELOPMENT_SECRET_KEY_CHANGE_IT_LATER_123456789",
        "DEVELOPMENT_ONLY_SECRET_KEY_NEVER_USE_IN_PRODUCTION_0123456789"
    ];

    public static string Resolve(string? configuredKey, IHostEnvironment environment)
    {
        var isLocal = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (!isLocal)
            {
                throw new InvalidOperationException(
                    "JWT secret key is not configured. Set the Jwt__SecretKey environment variable " +
                    $"to at least {MinimumKeyBytes} random characters.");
            }

            // Local runs without a configured key get a random one; tokens stop working after a restart.
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }

        if (Encoding.UTF8.GetByteCount(configuredKey) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT secret key must be at least {MinimumKeyBytes} bytes long.");
        }

        if (!isLocal && KnownPublicKeys.Contains(configuredKey, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "JWT secret key is a publicly known development key. Configure a new random key for this environment.");
        }

        return configuredKey;
    }
}
