using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Messenger.IntegrationTests;

public sealed class RealtimeDeliveryTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _admin;

    public RealtimeDeliveryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
    }

    [Fact]
    public async Task Member_Should_Receive_New_Message_Without_Opening_The_Conversation()
    {
        var sender = await CreateUserAsync("Sender");
        var receiver = await CreateUserAsync("Receiver");
        var conversationId = await CreatePrivateConversationAsync(sender, receiver);

        var messageCreated = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var conversationUpdated = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The receiver is connected but has not called JoinConversation (e.g. looking at the chat list).
        await using var connection = CreateHubConnection(receiver);
        connection.On<JsonElement>("MessageCreated", payload => messageCreated.TrySetResult(payload));
        connection.On<JsonElement>("ConversationUpdated", payload => conversationUpdated.TrySetResult(payload));
        await connection.StartAsync();

        var response = await CreateApiClient(sender).PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), "Hello"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var created = await messageCreated.Task.WaitAsync(EventTimeout);
        created.GetProperty("conversationId").GetGuid().Should().Be(conversationId);

        var updated = await conversationUpdated.Task.WaitAsync(EventTimeout);
        updated.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
    }

    [Fact]
    public async Task Member_Who_Left_Should_Not_Receive_New_Messages_In_Real_Time()
    {
        var owner = await CreateUserAsync("Owner");
        var stays = await CreateUserAsync("Stays");
        var leaves = await CreateUserAsync("Leaves");
        var conversationId = await CreateGroupAsync(owner, [stays, leaves]);

        var leaveResponse = await CreateApiClient(leaves).PostAsync($"/api/v1/conversations/{conversationId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.OK, await leaveResponse.Content.ReadAsStringAsync());

        var leftUserReceived = false;
        var stayingUserReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var leftConnection = CreateHubConnection(leaves);
        leftConnection.On<JsonElement>("MessageCreated", _ => leftUserReceived = true);
        await leftConnection.StartAsync();

        await using var stayingConnection = CreateHubConnection(stays);
        stayingConnection.On<JsonElement>("MessageCreated", _ => stayingUserReceived.TrySetResult());
        await stayingConnection.StartAsync();

        var response = await CreateApiClient(owner).PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), "Team only"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        // Once the staying member has the message, the one who left would have it too if it were sent.
        await stayingUserReceived.Task.WaitAsync(EventTimeout);
        await Task.Delay(200);

        leftUserReceived.Should().BeFalse();
    }

    private HubConnection CreateHubConnection(Guid userId)
    {
        var server = _factory.Server;

        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Headers["X-Test-UserId"] = userId.ToString();
            })
            .Build();
    }

    private HttpClient CreateApiClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _admin.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreatePrivateConversationAsync(Guid firstUserId, Guid secondUserId)
    {
        var response = await _admin.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(firstUserId, secondUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreateGroupAsync(Guid createdBy, List<Guid> members)
    {
        var response = await _admin.PostAsJsonAsync(
            "/api/v1/conversations/group",
            new CreateGroupConversationRequest("Team", createdBy, members));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);
}
