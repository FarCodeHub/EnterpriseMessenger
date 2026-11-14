using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Realtime;
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
        connection.On<JsonElement>(RealtimeEvents.MessageCreated, payload => messageCreated.TrySetResult(payload));
        connection.On<JsonElement>(RealtimeEvents.ConversationUpdated, payload => conversationUpdated.TrySetResult(payload));
        await connection.StartAsync();

        var response = await CreateApiClient(sender).PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), "Hello"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var created = await messageCreated.Task.WaitAsync(EventTimeout);
        created.GetProperty("conversationId").GetGuid().Should().Be(conversationId);

        var updated = await conversationUpdated.Task.WaitAsync(EventTimeout);
        updated.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        updated.GetProperty("senderDisplayName").GetString().Should().Be("Sender");
    }

    [Fact]
    public async Task Hub_Returns_The_Server_Time_For_Clock_Offset_Estimates()
    {
        var user = await CreateUserAsync("Clock");
        await using var connection = CreateHubConnection(user);
        await connection.StartAsync();

        var before = DateTime.UtcNow;
        var serverTime = await connection.InvokeAsync<DateTime>("GetServerTime");

        serverTime.Kind.Should().Be(DateTimeKind.Utc);
        serverTime.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Theory]
    [InlineData("/api/v1/messages/forward")]
    [InlineData("/api/v1/messages/forward-batch")]
    public async Task Forward_Should_Update_The_Conversation_List_With_The_Forwarders_Name(string endpoint)
    {
        var author = await CreateUserAsync("Author");
        var forwarder = await CreateUserAsync("Forwarder");
        var receiver = await CreateUserAsync("Receiver");
        var source = await CreatePrivateConversationAsync(author, forwarder);
        var target = await CreatePrivateConversationAsync(forwarder, receiver);

        var sent = await CreateApiClient(author).PostAsJsonAsync(
            "/api/v1/messages", new SendMessageRequest(source, Guid.NewGuid(), "Original"));
        var sourceMessageId = (await sent.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var conversationUpdated = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = CreateHubConnection(receiver);
        connection.On<JsonElement>(RealtimeEvents.ConversationUpdated, payload => conversationUpdated.TrySetResult(payload));
        await connection.StartAsync();

        object request = endpoint.EndsWith("batch")
            ? new ForwardMessagesRequest([sourceMessageId], [target], null)
            : new ForwardMessageRequest(sourceMessageId, [target], null);

        var response = await CreateApiClient(forwarder).PostAsJsonAsync(endpoint, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var updated = await conversationUpdated.Task.WaitAsync(EventTimeout);
        updated.GetProperty("conversationId").GetGuid().Should().Be(target);
        updated.GetProperty("senderDisplayName").GetString().Should().Be("Forwarder");
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
        leftConnection.On<JsonElement>(RealtimeEvents.MessageCreated, _ => leftUserReceived = true);
        await leftConnection.StartAsync();

        await using var stayingConnection = CreateHubConnection(stays);
        stayingConnection.On<JsonElement>(RealtimeEvents.MessageCreated, _ => stayingUserReceived.TrySetResult());
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
