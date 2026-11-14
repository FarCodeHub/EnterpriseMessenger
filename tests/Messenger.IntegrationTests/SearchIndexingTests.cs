using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Messenger.Application.Search;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;
using Messenger.Domain.Messages;
using Messenger.Infrastructure.Consumers;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

/// <summary>Editing and deleting a message write an outbox event; the request itself never touches the index.</summary>
public sealed class MessageChangeOutboxTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MessageChangeOutboxTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Edit_And_Delete_Write_Outbox_Events()
    {
        var sender = await CreateUserAsync("Sender");
        var receiver = await CreateUserAsync("Receiver");
        SetCurrentUser(sender);

        var conversation = await _client.PostAsJsonAsync(
            "/api/v1/conversations/private", new CreatePrivateConversationRequest(sender, receiver));
        var conversationId = (await conversation.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var sent = await _client.PostAsJsonAsync(
            "/api/v1/messages", new SendMessageRequest(conversationId, Guid.NewGuid(), "draft"));
        var messageId = (await sent.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        (await _client.PutAsJsonAsync($"/api/v1/messages/{messageId}", new EditMessageRequest("final")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.DeleteAsync($"/api/v1/messages/{messageId}?userId={sender}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var events = (await dbContext.OutboxMessages.ToListAsync())
            .Where(x => x.Payload.Contains(messageId.ToString()))
            .ToList();

        var edited = events.Single(x => x.Type.StartsWith(typeof(MessageEditedIntegrationEvent).FullName!));
        JsonSerializer.Deserialize<MessageEditedIntegrationEvent>(edited.Payload)!
            .Should().Match<MessageEditedIntegrationEvent>(x =>
                x.MessageId == messageId && x.ConversationId == conversationId && x.Body == "final");

        var deleted = events.Single(x => x.Type.StartsWith(typeof(MessageDeletedIntegrationEvent).FullName!));
        JsonSerializer.Deserialize<MessageDeletedIntegrationEvent>(deleted.Payload)!
            .MessageId.Should().Be(messageId);
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/users", new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private void SetCurrentUser(Guid userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }

    private sealed record IdResponse(Guid Id);
}

/// <summary>
/// <see cref="MessageSearchIndexingConsumer"/> on an in-memory bus: whatever the event and its order, the index ends
/// up with the message as it is in the database.
/// </summary>
public sealed class MessageSearchIndexingConsumerTests : IAsyncLifetime
{
    private readonly RecordingSearchIndexer _indexer = new();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var databaseName = $"indexing-{Guid.NewGuid()}";

        _provider = new ServiceCollection()
            .AddDbContext<MessengerDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddSingleton<IMessageSearchIndexer>(_indexer)
            .AddMassTransitTestHarness(x => x.AddConsumer<MessageSearchIndexingConsumer>())
            .BuildServiceProvider(validateScopes: true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task Late_Created_Event_Indexes_The_Edited_Text()
    {
        var message = await StoreAsync("draft", m => m.Edit("final"));

        // The edit's event is handled first, then the older create event arrives.
        await PublishAndWaitAsync(new MessageEditedIntegrationEvent(
            message.Id, message.ConversationId, message.SequenceNumber, message.SenderUserId, "final", DateTime.UtcNow));
        await PublishAndWaitAsync(new MessageCreatedIntegrationEvent(
            message.Id, message.ConversationId, message.SequenceNumber, message.SenderUserId, "draft",
            message.CreatedAtUtc, null, null, null));

        _indexer.Indexed.Should().HaveCount(2).And.OnlyContain(x => x.Id == message.Id && x.Body == "final");
        _indexer.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleted_Message_Is_Removed_Even_When_Its_Create_Event_Comes_Later()
    {
        var message = await StoreAsync("secret", m => m.Delete());

        await PublishAndWaitAsync(new MessageDeletedIntegrationEvent(
            message.Id, message.ConversationId, message.SequenceNumber, DateTime.UtcNow));
        await PublishAndWaitAsync(new MessageCreatedIntegrationEvent(
            message.Id, message.ConversationId, message.SequenceNumber, message.SenderUserId, "secret",
            message.CreatedAtUtc, null, null, null));

        _indexer.Indexed.Should().BeEmpty();
        _indexer.Deleted.Should().Equal(message.Id, message.Id);
    }

    [Fact]
    public async Task Unknown_Message_Is_Removed_From_The_Index()
    {
        var missingId = Guid.NewGuid();

        await PublishAndWaitAsync(new MessageEditedIntegrationEvent(
            missingId, Guid.NewGuid(), 1, Guid.NewGuid(), "gone", DateTime.UtcNow));

        _indexer.Deleted.Should().Equal(missingId);
    }

    private async Task<Message> StoreAsync(string body, Action<Message> change)
    {
        var message = new Message(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), body);
        change(message);

        await using var scope = _provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();

        return message;
    }

    private async Task PublishAndWaitAsync<T>(T integrationEvent) where T : class
    {
        var callsBefore = _indexer.Calls;

        await _harness.Bus.Publish(integrationEvent);

        // Wait for the consumer to reach the index (the harness stops waiting once the bus has been idle).
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (_indexer.Calls == callsBefore && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        _indexer.Calls.Should().Be(callsBefore + 1, "the consumer handles {0} exactly once", typeof(T).Name);
        (await _harness.Published.Any<Fault<T>>()).Should().BeFalse();
    }

    private sealed class RecordingSearchIndexer : IMessageSearchIndexer
    {
        public List<MessageSearchDocument> Indexed { get; } = [];

        public List<Guid> Deleted { get; } = [];

        public int Calls
        {
            get
            {
                lock (Indexed)
                lock (Deleted)
                    return Indexed.Count + Deleted.Count;
            }
        }

        public Task IndexAsync(MessageSearchDocument document, CancellationToken cancellationToken)
        {
            lock (Indexed)
                Indexed.Add(document);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid messageId, CancellationToken cancellationToken)
        {
            lock (Deleted)
                Deleted.Add(messageId);

            return Task.CompletedTask;
        }

        public Task EnsureIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RebuildAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
