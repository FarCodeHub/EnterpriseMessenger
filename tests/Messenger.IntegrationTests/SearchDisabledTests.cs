using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Application.Search;
using Messenger.Contracts.Common;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Search;
using Messenger.Contracts.Users;
using Messenger.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

/// <summary>
/// Without OpenSearch the messenger must still send, edit, delete and search messages.
/// Before the fallback was added, every MessagesController endpoint failed: MessageEditor and
/// MessageDeleter need IMessageSearchIndexer, which was only registered when OpenSearch:Url was set.
/// </summary>
public sealed class SearchDisabledTests : IClassFixture<SearchDisabledWebApplicationFactory>
{
    private readonly SearchDisabledWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SearchDisabledTests(SearchDisabledWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void Registers_Fallback_Search_Services()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMessageSearchIndexer>()
            .Should().BeOfType<NoOpMessageSearchIndexer>();
        scope.ServiceProvider.GetRequiredService<IMessageFullTextSearchService>()
            .Should().BeOfType<PostgresMessageFullTextSearchService>();
    }

    [Fact]
    public async Task Send_Edit_And_Delete_Work()
    {
        var (senderId, conversationId) = await CreateConversationAsync();

        var message = await SendMessageAsync(conversationId, "first");

        var editResponse = await _client.PutAsJsonAsync(
            $"/api/v1/messages/{message.Id}",
            new EditMessageRequest("first, edited"));
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK, await editResponse.Content.ReadAsStringAsync());

        var deleteResponse = await _client.DeleteAsync($"/api/v1/messages/{message.Id}?userId={senderId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK, await deleteResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_Uses_Postgres_Normalizes_Persian_And_Skips_Deleted_Messages()
    {
        var (senderId, conversationId) = await CreateConversationAsync();

        // Arabic Kaf/Yeh in the stored text, Persian forms in the query.
        var kept = await SendMessageAsync(conversationId, "گزارش مالي كامل شد");
        var deleted = await SendMessageAsync(conversationId, "گزارش مالی حذف‌شدنی");
        await _client.DeleteAsync($"/api/v1/messages/{deleted.Id}?userId={senderId}");

        var response = await _client.GetAsync(
            $"/api/v1/search/messages?query={Uri.EscapeDataString("مالی کامل")}&conversationId={conversationId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var result = await response.Content.ReadFromJsonAsync<PagedResponse<MessageSearchResponse>>();
        result!.Items.Select(x => x.Id).Should().Equal(kept.Id);

        var all = await _client.GetFromJsonAsync<PagedResponse<MessageSearchResponse>>(
            $"/api/v1/search/messages?query={Uri.EscapeDataString("گزارش")}&conversationId={conversationId}");
        all!.Items.Select(x => x.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Search_Returns_Only_Conversations_Of_The_Current_User()
    {
        var (_, conversationId) = await CreateConversationAsync();
        var secret = $"secret-{Guid.NewGuid():N}";
        await SendMessageAsync(conversationId, secret);

        var outsiderId = await CreateUserAsync("Outsider");
        SetCurrentUser(outsiderId);

        var result = await _client.GetFromJsonAsync<PagedResponse<MessageSearchResponse>>(
            $"/api/v1/search/messages?query={secret}");

        result!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Rebuild_Index_Reports_That_Search_Is_Not_Configured()
    {
        var response = await _client.PostAsync("/api/v1/search/messages/rebuild-index", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(Guid SenderId, Guid ConversationId)> CreateConversationAsync()
    {
        var senderId = await CreateUserAsync("Sender");
        var receiverId = await CreateUserAsync("Receiver");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(senderId, receiverId));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var conversation = await response.Content.ReadFromJsonAsync<IdResponse>();
        SetCurrentUser(senderId);

        return (senderId, conversation!.Id);
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<IdResponse> SendMessageAsync(Guid conversationId, string body)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), body, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!;
    }

    private void SetCurrentUser(Guid userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }

    private sealed record IdResponse(Guid Id);
}
