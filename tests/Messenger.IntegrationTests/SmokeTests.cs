using FluentAssertions;

namespace Messenger.IntegrationTests;

public sealed class SmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SmokeTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUsers_Should_Return_Success()
    {
        var response = await _client.GetAsync("/api/v1/users");

        var content = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode
            .Should()
            .BeTrue($"StatusCode: {response.StatusCode}, Content: {content}");
    }
}