using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Messenger.IntegrationTests;

/// <summary>
/// Rebuilding the index runs in the background: on 500k messages it took longer than nginx waits (60 s), and the
/// request was cancelled with only a few thousand messages indexed.
/// </summary>
public sealed class SearchRebuildTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SearchRebuildTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task Rebuild_Starts_In_The_Background_And_Reports_Its_Status()
    {
        var start = await _client.PostAsync("/api/v1/search/messages/rebuild-index", null);
        start.StatusCode.Should().Be(HttpStatusCode.Accepted, await start.Content.ReadAsStringAsync());

        RebuildStatus? status = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            status = await _client.GetFromJsonAsync<RebuildStatus>("/api/v1/search/messages/rebuild-index");

            if (status!.State != "running")
                break;

            await Task.Delay(50);
        }

        status!.State.Should().Be("completed");
        status.StartedAtUtc.Should().NotBeNull();
        status.FinishedAtUtc.Should().NotBeNull();
        status.Error.Should().BeNull();
    }

    [Fact]
    public async Task Only_Admins_May_Rebuild()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/search/messages/rebuild-index");
        request.Headers.Add(TestAuthHandler.RoleHeaderName, "User");

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record RebuildStatus(
        string State, long Indexed, DateTime? StartedAtUtc, DateTime? FinishedAtUtc, string? Error);
}
