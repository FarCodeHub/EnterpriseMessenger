using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messenger.IntegrationTests;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RoleHeaderName = "X-Test-Role";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var headerUserId)
            ? headerUserId.ToString()
            : Guid.Empty.ToString();

        // Tests run as an admin unless they ask for another role with the X-Test-Role header.
        var role = Request.Headers.TryGetValue(RoleHeaderName, out var headerRole)
            ? headerRole.ToString()
            : "Admin";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            // One name per user, like real tokens: the per-user rate limits are keyed by it.
            new Claim(ClaimTypes.Name, $"test-user-{userId}"),
            new Claim(ClaimTypes.Role, role),
            new Claim("display_name", "Test User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}