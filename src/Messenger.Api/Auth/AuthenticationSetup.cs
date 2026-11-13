using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Messenger.Application.Auth.External;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Messenger.Api.Auth;

/// <summary>
/// Settings for signing in through the organization's identity provider (Keycloak, federated with LDAP/AD).
/// </summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>Public realm URL, e.g. https://chat.example.com/auth/realms/messenger. It is also the token issuer.</summary>
    public string? Authority { get; set; }

    /// <summary>Optional internal discovery URL when the API reaches Keycloak by another host name (e.g. inside Docker).</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>Audience the API requires in access tokens.</summary>
    public string Audience { get; set; } = "messenger-api";

    /// <summary>Public OIDC client used by the web app.</summary>
    public string ClientId { get; set; } = "messenger-web";

    /// <summary>Realm role that makes a user an admin in the messenger.</summary>
    public string AdminRole { get; set; } = "messenger-admin";

    public bool RequireHttpsMetadata { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority);
}

/// <summary>
/// The sign-in modes this instance accepts. Exposed to the web app through <c>GET /api/v1/auth/config</c>.
/// </summary>
public sealed record AuthModeSettings(
    bool LocalLoginEnabled,
    KeycloakOptions Keycloak);

public static class AuthenticationSetup
{
    public const string SelectorScheme = "Bearer";
    public const string LocalScheme = "LocalJwt";
    public const string KeycloakScheme = "Keycloak";

    private const string HubPath = "/hubs/chat";
    private static readonly TimeSpan UserCacheDuration = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddMessengerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var keycloak = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()
            ?? new KeycloakOptions();

        var isLocalEnvironment = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var allowLocalLogin = configuration.GetValue<bool>("Auth:AllowLocalLogin");
        var localLoginEnabled = isLocalEnvironment || allowLocalLogin;

        if (!keycloak.IsConfigured && !localLoginEnabled)
        {
            throw new InvalidOperationException(
                "No sign-in method is configured. Set Keycloak__Authority to the Keycloak realm URL. " +
                "(Auth__AllowLocalLogin=true re-enables the username-only login, which is not secure.)");
        }

        services.AddSingleton(new AuthModeSettings(localLoginEnabled, keycloak));
        services.AddMemoryCache();

        var authentication = services.AddAuthentication(SelectorScheme);

        authentication.AddPolicyScheme(SelectorScheme, SelectorScheme, options =>
        {
            options.ForwardDefaultSelector = context =>
                SelectScheme(context, keycloak, localLoginEnabled);
        });

        if (localLoginEnabled)
            AddLocalJwt(authentication, configuration, environment);

        if (keycloak.IsConfigured)
            AddKeycloakJwt(authentication, keycloak);

        return services;
    }

    private static string SelectScheme(
        HttpContext context,
        KeycloakOptions keycloak,
        bool localLoginEnabled)
    {
        if (!keycloak.IsConfigured)
            return LocalScheme;

        if (!localLoginEnabled)
            return KeycloakScheme;

        var token = ReadToken(context.Request);

        if (token is null)
            return KeycloakScheme;

        try
        {
            var issuer = new JsonWebTokenHandler().ReadJsonWebToken(token).Issuer;

            return SameIssuer(issuer, keycloak.Authority) ? KeycloakScheme : LocalScheme;
        }
        catch (ArgumentException)
        {
            // Not a JWT; let the Keycloak handler reject it.
            return KeycloakScheme;
        }
    }

    private static void AddLocalJwt(
        AuthenticationBuilder authentication,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwtSecretKey = JwtSecretKeyResolver.Resolve(
            configuration["Jwt:SecretKey"],
            environment);

        // JwtTokenService reads the key from configuration, so make sure it sees the resolved one.
        configuration["Jwt:SecretKey"] = jwtSecretKey;

        authentication.AddJwtBearer(LocalScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecretKey))
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ReadHubAccessToken
            };
        });
    }

    private static void AddKeycloakJwt(
        AuthenticationBuilder authentication,
        KeycloakOptions keycloak)
    {
        authentication.AddJwtBearer(KeycloakScheme, options =>
        {
            options.Authority = keycloak.Authority;
            options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
            options.MapInboundClaims = false;

            if (!string.IsNullOrWhiteSpace(keycloak.MetadataAddress))
                options.MetadataAddress = keycloak.MetadataAddress;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = keycloak.Authority!.TrimEnd('/'),
                ValidateAudience = true,
                ValidAudience = keycloak.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ReadHubAccessToken,
                OnTokenValidated = context => MapKeycloakUserAsync(context, keycloak)
            };
        });
    }

    /// <summary>
    /// Replaces the Keycloak identity with the local messenger user, so the rest of the API keeps working with
    /// <see cref="ClaimTypes.NameIdentifier"/> = local user id and the Admin/User role.
    /// </summary>
    private static async Task MapKeycloakUserAsync(
        TokenValidatedContext context,
        KeycloakOptions keycloak)
    {
        var principal = context.Principal!;
        var subject = principal.FindFirstValue("sub");
        var userName = principal.FindFirstValue("preferred_username");
        var displayName = principal.FindFirstValue("name");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(userName))
        {
            context.Fail("Token is missing the sub or preferred_username claim.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();

        var user = await cache.GetOrCreateAsync($"external-user:{subject}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = UserCacheDuration;

            var provisioner = services.GetRequiredService<IExternalUserProvisioner>();

            return await provisioner.ProvisionAsync(
                new ExternalUserCommand(subject, userName, displayName),
                context.HttpContext.RequestAborted);
        });

        if (user is null || !user.IsActive)
        {
            context.Fail("User is not active.");
            return;
        }

        var isAdmin = user.IsAdmin || GetRealmRoles(principal).Contains(keycloak.AdminRole);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, isAdmin ? "Admin" : "User"),
            new("display_name", user.DisplayName),
            new("external_sub", subject)
        };

        context.Principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, KeycloakScheme, ClaimTypes.Name, ClaimTypes.Role));
    }

    private static HashSet<string> GetRealmRoles(ClaimsPrincipal principal)
    {
        var roles = new HashSet<string>(StringComparer.Ordinal);
        var realmAccess = principal.FindFirstValue("realm_access");

        if (string.IsNullOrWhiteSpace(realmAccess))
            return roles;

        try
        {
            using var document = JsonDocument.Parse(realmAccess);

            if (document.RootElement.TryGetProperty("roles", out var roleArray) &&
                roleArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in roleArray.EnumerateArray())
                {
                    if (role.GetString() is { } value)
                        roles.Add(value);
                }
            }
        }
        catch (JsonException)
        {
            // Ignore a malformed claim; the user simply gets no extra roles.
        }

        return roles;
    }

    private static Task ReadHubAccessToken(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"];

        if (!string.IsNullOrWhiteSpace(accessToken) &&
            context.HttpContext.Request.Path.StartsWithSegments(HubPath))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }

    private static string? ReadToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header["Bearer ".Length..].Trim();

        if (request.Path.StartsWithSegments(HubPath))
        {
            var accessToken = request.Query["access_token"].ToString();
            return string.IsNullOrWhiteSpace(accessToken) ? null : accessToken;
        }

        return null;
    }

    private static bool SameIssuer(string? issuer, string? authority) =>
        issuer is not null &&
        authority is not null &&
        string.Equals(issuer.TrimEnd('/'), authority.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
