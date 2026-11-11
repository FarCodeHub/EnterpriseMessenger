using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Users;

namespace Messenger.IntegrationTests;

public sealed class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateUser_Should_Return_Forbidden_For_Non_Admin()
    {
        var client = CreateClient(Guid.NewGuid(), role: "User");

        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest("Someone", $"user-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSyncEvents_Should_Return_Forbidden_For_Another_User()
    {
        var client = CreateClient(Guid.NewGuid(), role: "Admin");

        var response = await client.GetAsync(
            $"/api/v1/sync/{Guid.NewGuid()}/device-001?afterEventId=0&take=10");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserConversations_Should_Return_Forbidden_For_Another_User()
    {
        var client = CreateClient(Guid.NewGuid(), role: "User");

        var response = await client.GetAsync(
            $"/api/v1/conversations/user/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegisterDevice_Should_Return_Forbidden_For_Another_User_When_Not_Admin()
    {
        var client = CreateClient(Guid.NewGuid(), role: "User");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{Guid.NewGuid()}/devices",
            new RegisterDeviceRequest("device-001", "Test Device"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateClient(Guid userId, string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, role);
        return client;
    }
}
