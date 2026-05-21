using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Auth;
using Messenger.Contracts.Users;

namespace Messenger.IntegrationTests;

public sealed class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task Login_Should_Return_AccessToken_And_RefreshToken()
    {
        var userName = $"auth-user-{Guid.NewGuid():N}";

        await _client.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest("Auth User", userName));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(userName, "web-001"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("accessToken");
        body.Should().Contain("refreshToken");
    }

    [Fact]
    public async Task Refresh_Should_Revoke_Old_RefreshToken_And_Return_New_One()
    {
        var userName = $"refresh-user-{Guid.NewGuid():N}";

        await _client.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest("Refresh User", userName));

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(userName, "web-001"));

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginResult.Should().NotBeNull();

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(loginResult!.RefreshToken, "web-001"));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondRefreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(loginResult.RefreshToken, "web-001"));

        secondRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record AuthResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresInSeconds);
}