using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Users;

namespace Messenger.IntegrationTests;

public sealed class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add(
    "X-Test-UserId",
    Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task CreateUser_Should_Create_User()
    {
        var request = new CreateUserRequest(
            DisplayName: "Farhad Salehi",
            UserName: $"farhad-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/v1/users", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(request.DisplayName);
        body.Should().Contain(request.UserName);
    }

    [Fact]
    public async Task CreateUser_Should_Return_Conflict_When_UserName_Exists()
    {
        var userName = $"duplicate-{Guid.NewGuid():N}";

        var request = new CreateUserRequest(
            DisplayName: "Test User",
            UserName: userName);

        var firstResponse = await _client.PostAsJsonAsync("/api/v1/users", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await _client.PostAsJsonAsync("/api/v1/users", request);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}