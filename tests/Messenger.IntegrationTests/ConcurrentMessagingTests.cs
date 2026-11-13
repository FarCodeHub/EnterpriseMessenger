using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

/// <summary>Concurrent sends against PostgreSQL: what users do in a busy group.</summary>
public sealed class ConcurrentMessagingTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public ConcurrentMessagingTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [PostgresFact]
    public async Task Concurrent_Sends_Should_All_Succeed_With_Unique_Consecutive_Sequence_Numbers()
    {
        var admin = CreateClient(Guid.NewGuid());
        var first = await CreateUserAsync(admin, "First");
        var second = await CreateUserAsync(admin, "Second");
        var conversationId = await CreatePrivateConversationAsync(admin, first, second);

        var sender = CreateClient(first);

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            sender.PostAsJsonAsync(
                "/api/v1/messages",
                new SendMessageRequest(conversationId, Guid.NewGuid(), $"Message {i}"))));

        foreach (var response in responses)
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var sequenceNumbers = new List<long>();

        foreach (var response in responses)
            sequenceNumbers.Add((await response.Content.ReadFromJsonAsync<SendResponse>())!.SequenceNumber);

        sequenceNumbers.Order().Should().Equal(Enumerable.Range(1, 30).Select(x => (long)x));
    }

    [PostgresFact]
    public async Task Concurrent_Retries_Of_One_Message_Should_Store_It_Once()
    {
        var admin = CreateClient(Guid.NewGuid());
        var first = await CreateUserAsync(admin, "First");
        var second = await CreateUserAsync(admin, "Second");
        var conversationId = await CreatePrivateConversationAsync(admin, first, second);

        var sender = CreateClient(first);
        var clientMessageId = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            sender.PostAsJsonAsync(
                "/api/v1/messages",
                new SendMessageRequest(conversationId, clientMessageId, "Same message"))));

        foreach (var response in responses)
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var ids = new HashSet<Guid>();

        foreach (var response in responses)
            ids.Add((await response.Content.ReadFromJsonAsync<SendResponse>())!.Id);

        ids.Should().HaveCount(1);
    }

    [PostgresFact]
    public async Task Concurrent_Sends_In_Different_Conversations_Should_Give_A_Shared_Member_Unique_Sync_Event_Ids()
    {
        var admin = CreateClient(Guid.NewGuid());
        var shared = await CreateUserAsync(admin, "Shared");
        var others = new List<Guid>();

        for (var i = 0; i < 5; i++)
            others.Add(await CreateUserAsync(admin, $"Other {i}"));

        var conversations = new List<(Guid ConversationId, Guid Sender)>();

        foreach (var other in others)
            conversations.Add((await CreatePrivateConversationAsync(admin, shared, other), other));

        var responses = await Task.WhenAll(conversations.SelectMany(c => Enumerable.Range(0, 4).Select(i =>
            CreateClient(c.Sender).PostAsJsonAsync(
                "/api/v1/messages",
                new SendMessageRequest(c.ConversationId, Guid.NewGuid(), $"Hello {i}")))));

        foreach (var response in responses)
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var eventIds = await dbContext.UserSyncEvents
            .Where(x => x.UserId == shared)
            .Select(x => x.EventId)
            .ToListAsync();

        eventIds.Should().HaveCount(20).And.OnlyHaveUniqueItems();
    }

    [PostgresFact]
    public async Task Concurrent_Sends_And_Forwards_Into_Shared_Conversations_Should_Not_Deadlock_Or_Skip_Numbers()
    {
        var admin = CreateClient(Guid.NewGuid());
        var alice = await CreateUserAsync(admin, "Alice");
        var bob = await CreateUserAsync(admin, "Bob");
        var carol = await CreateUserAsync(admin, "Carol");
        var withBob = await CreatePrivateConversationAsync(admin, alice, bob);
        var withCarol = await CreatePrivateConversationAsync(admin, alice, carol);

        var aliceClient = CreateClient(alice);
        var source = await aliceClient.PostAsJsonAsync(
            "/api/v1/messages", new SendMessageRequest(withBob, Guid.NewGuid(), "Source"));
        source.StatusCode.Should().Be(HttpStatusCode.OK, await source.Content.ReadAsStringAsync());
        var sourceId = (await source.Content.ReadFromJsonAsync<SendResponse>())!.Id;

        // Forwards lock both conversations (in opposite request order) while Bob and Carol send into them.
        var requests = new List<Task<HttpResponseMessage>>();

        for (var i = 0; i < 10; i++)
        {
            requests.Add(aliceClient.PostAsJsonAsync(
                "/api/v1/messages/forward-batch",
                new ForwardMessagesRequest([sourceId], [withBob, withCarol], null)));
            requests.Add(aliceClient.PostAsJsonAsync(
                "/api/v1/messages/forward",
                new ForwardMessageRequest(sourceId, [withCarol, withBob], null)));
            requests.Add(CreateClient(bob).PostAsJsonAsync(
                "/api/v1/messages", new SendMessageRequest(withBob, Guid.NewGuid(), $"Bob {i}")));
            requests.Add(CreateClient(carol).PostAsJsonAsync(
                "/api/v1/messages", new SendMessageRequest(withCarol, Guid.NewGuid(), $"Carol {i}")));
        }

        foreach (var response in await Task.WhenAll(requests))
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        // 1 source + 20 forwards + 10 sends with Bob; 20 forwards + 10 sends with Carol.
        (await SequenceNumbersAsync(dbContext, withBob)).Should().Equal(Enumerable.Range(1, 31).Select(x => (long)x));
        (await SequenceNumbersAsync(dbContext, withCarol)).Should().Equal(Enumerable.Range(1, 30).Select(x => (long)x));

        (await dbContext.UserSyncEvents.Where(x => x.UserId == alice).Select(x => x.EventId).ToListAsync())
            .Should().HaveCount(61).And.OnlyHaveUniqueItems();
    }

    private static Task<List<long>> SequenceNumbersAsync(MessengerDbContext dbContext, Guid conversationId) =>
        dbContext.Messages
            .Where(x => x.ConversationId == conversationId)
            .Select(x => x.SequenceNumber)
            .OrderBy(x => x)
            .ToListAsync();

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<Guid> CreatePrivateConversationAsync(HttpClient client, Guid firstUserId, Guid secondUserId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(firstUserId, secondUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record SendResponse(Guid Id, long SequenceNumber);
}
