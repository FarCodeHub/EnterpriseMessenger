using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Common;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Search;
using Messenger.Contracts.Users;
using Microsoft.AspNetCore.Hosting;

namespace Messenger.IntegrationTests;

/// <summary>The real PostgreSQL app without OpenSearch and with the real (fallback) search services.</summary>
public sealed class PostgresSearchDisabledWebApplicationFactory : PostgresWebApplicationFactory
{
    protected override bool UseFakeSearch => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        SearchDisabledWebApplicationFactory.DisableOpenSearch(builder);
    }
}

/// <summary>
/// The fallback search query must translate to SQL; the in-memory provider used by
/// <see cref="SearchDisabledTests"/> evaluates it in .NET and cannot show that.
/// </summary>
public sealed class PostgresFallbackSearchTests : IClassFixture<PostgresSearchDisabledWebApplicationFactory>
{
    private readonly PostgresSearchDisabledWebApplicationFactory _factory;

    public PostgresFallbackSearchTests(PostgresSearchDisabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Search_Finds_Messages_With_Normalized_Persian_Text_In_Own_Conversations_Only()
    {
        var admin = CreateClient(Guid.NewGuid());
        var senderId = await CreateUserAsync(admin, "Sender");
        var receiverId = await CreateUserAsync(admin, "Receiver");
        var outsiderId = await CreateUserAsync(admin, "Outsider");

        var conversationResponse = await admin.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(senderId, receiverId));
        conversationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversationId = (await conversationResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var sender = CreateClient(senderId);
        var kept = await SendAsync(sender, conversationId, "گزارش مالي كامل شد");
        var deleted = await SendAsync(sender, conversationId, "گزارش مالی حذف‌شدنی");
        (await sender.DeleteAsync($"/api/v1/messages/{deleted.Id}?userId={senderId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var query = Uri.EscapeDataString("مالی");

        var senderResult = await sender.GetFromJsonAsync<PagedResponse<MessageSearchResponse>>(
            $"/api/v1/search/messages?query={query}");
        senderResult!.Items.Select(x => x.Id).Should().Equal(kept.Id);
        senderResult.TotalCount.Should().Be(1);

        var outsiderResult = await CreateClient(outsiderId).GetFromJsonAsync<PagedResponse<MessageSearchResponse>>(
            $"/api/v1/search/messages?query={query}");
        outsiderResult!.TotalCount.Should().Be(0);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static async Task<Guid> CreateUserAsync(HttpClient admin, string displayName)
    {
        var response = await admin.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<IdResponse> SendAsync(HttpClient sender, Guid conversationId, string body)
    {
        var response = await sender.PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), body));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!;
    }

    private sealed record IdResponse(Guid Id);
}
