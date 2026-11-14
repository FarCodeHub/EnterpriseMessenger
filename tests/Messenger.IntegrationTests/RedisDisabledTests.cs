using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Messenger.Application.Abstractions.Realtime;
using Messenger.Application.Realtime;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Realtime;
using Messenger.Contracts.Users;
using Messenger.Infrastructure.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

/// <summary>
/// The app without Redis and OpenSearch, with the real fallback services instead of test fakes and every DI
/// registration validated at startup. Before the in-memory fallback, an empty Redis:ConnectionString stopped the
/// app from starting (RedisConnectionTracker needs a Redis connection, and IPresenceService was not registered).
/// </summary>
public sealed class NoOptionalServicesWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool UseFakeSearch => false;

    protected override bool UseFakeRealtime => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Redis:ConnectionString", "");
        SearchDisabledWebApplicationFactory.DisableOpenSearch(builder);
    }
}

public sealed class RedisDisabledTests : IClassFixture<NoOptionalServicesWebApplicationFactory>
{
    private readonly NoOptionalServicesWebApplicationFactory _factory;
    private readonly HttpClient _admin;

    public RedisDisabledTests(NoOptionalServicesWebApplicationFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClient();
    }

    [Fact]
    public void Registers_In_Memory_Presence_And_Connection_Tracking()
    {
        _factory.Services.GetRequiredService<IPresenceService>().Should().BeOfType<InMemoryPresenceService>();
        _factory.Services.GetRequiredService<IConnectionTracker>().Should().BeOfType<InMemoryConnectionTracker>();
    }

    [Fact]
    public async Task Connected_Member_Receives_Messages_And_Is_Online()
    {
        var sender = await CreateUserAsync("Sender");
        var receiver = await CreateUserAsync("Receiver");

        var conversation = await _admin.PostAsJsonAsync(
            "/api/v1/conversations/private", new CreatePrivateConversationRequest(sender, receiver));
        conversation.StatusCode.Should().Be(HttpStatusCode.OK, await conversation.Content.ReadAsStringAsync());
        var conversationId = (await conversation.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var messageCreated = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-UserId", receiver.ToString());
            })
            .Build();
        connection.On<JsonElement>(RealtimeEvents.MessageCreated, payload => messageCreated.TrySetResult(payload));
        await connection.StartAsync();
        await connection.InvokeAsync("JoinConversation", conversationId.ToString());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", sender.ToString());
        (await client.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(conversationId, Guid.NewGuid(), "Hi")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await messageCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        created.GetProperty("conversationId").GetGuid().Should().Be(conversationId);

        var presence = _factory.Services.GetRequiredService<IPresenceService>();
        (await presence.IsOnlineAsync(receiver, CancellationToken.None)).Should().BeTrue();
        (await presence.GetOnlineConversationUserIdsAsync(conversationId, CancellationToken.None))
            .Should().Contain(receiver);
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _admin.PostAsJsonAsync(
            "/api/v1/users", new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);
}

public sealed class InMemoryPresenceServiceTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly InMemoryPresenceService _presence;

    public InMemoryPresenceServiceTests() => _presence = new InMemoryPresenceService(_time);

    [Fact]
    public async Task User_Is_Online_Until_Their_Last_Connection_Closes()
    {
        var user = Guid.NewGuid();
        await _presence.SetOnlineAsync(user, "a", default);
        await _presence.SetOnlineAsync(user, "b", default);

        (await _presence.RemoveConnectionAsync(user, "a", default)).Should().BeFalse();
        (await _presence.IsOnlineAsync(user, default)).Should().BeTrue();
        (await _presence.RemoveConnectionAsync(user, "b", default)).Should().BeTrue();
        (await _presence.IsOnlineAsync(user, default)).Should().BeFalse();
    }

    [Fact]
    public async Task Conversation_Presence_Expires_Unless_Renewed()
    {
        var conversation = Guid.NewGuid();
        var user = Guid.NewGuid();
        await _presence.JoinConversationAsync(conversation, user, "a", default);

        _time.Advance(TimeSpan.FromSeconds(60));
        await _presence.RenewConversationPresenceAsync(conversation, user, "a", default);
        _time.Advance(TimeSpan.FromSeconds(60));
        (await _presence.GetOnlineConversationUserIdsAsync(conversation, default)).Should().Equal(user);
        (await _presence.GetConnectionConversationIdsAsync("a", default)).Should().Equal(conversation);

        _time.Advance(TimeSpan.FromSeconds(31));
        (await _presence.GetOnlineConversationUserIdsAsync(conversation, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Leaving_Reports_Whether_The_User_Is_Gone_From_The_Conversation()
    {
        var conversation = Guid.NewGuid();
        var user = Guid.NewGuid();
        await _presence.JoinConversationAsync(conversation, user, "a", default);
        await _presence.JoinConversationAsync(conversation, user, "b", default);

        (await _presence.LeaveConversationAsync(conversation, user, "a", default)).Should().BeFalse();
        (await _presence.LeaveConversationAsync(conversation, user, "b", default)).Should().BeTrue();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
