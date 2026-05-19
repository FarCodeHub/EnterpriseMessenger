using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;

namespace Messenger.IntegrationTests;

public sealed class MessagingFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MessagingFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessages_Should_Generate_Sequential_SequenceNumbers()
    {
        var user1Id = await CreateUserAsync("User 1");
        var user2Id = await CreateUserAsync("User 2");

        var conversationId = await CreatePrivateConversationAsync(user1Id, user2Id);

        var firstMessage = await SendMessageAsync(
            conversationId,
            user1Id,
            "11111111-1111-1111-1111-111111111111",
            "First message");

        firstMessage.SequenceNumber.Should().Be(1);

        var secondMessage = await SendMessageAsync(
            conversationId,
            user2Id,
            "22222222-2222-2222-2222-222222222222",
            "Second message");

        secondMessage.SequenceNumber.Should().Be(2);
    }


    [Fact]
public async Task SendMessage_Should_Return_Duplicate_When_ClientMessageId_Already_Exists()
{
    var user1Id = await CreateUserAsync("User 1");
    var user2Id = await CreateUserAsync("User 2");

    var conversationId = await CreatePrivateConversationAsync(user1Id, user2Id);

    var clientMessageId = Guid.NewGuid().ToString();

    var firstMessage = await SendMessageAsync(
        conversationId,
        user1Id,
        clientMessageId,
        "Original message");

    var duplicateMessage = await SendMessageAsync(
        conversationId,
        user1Id,
        clientMessageId,
        "Original message again");

    duplicateMessage.Id.Should().Be(firstMessage.Id);
    duplicateMessage.SequenceNumber.Should().Be(firstMessage.SequenceNumber);
    duplicateMessage.IsDuplicate.Should().BeTrue();
}


[Fact]
public async Task EditMessage_Should_Create_MessageEdited_SyncEvent()
{
    var user1Id = await CreateUserAsync("User 1");
    var user2Id = await CreateUserAsync("User 2");

    await RegisterDeviceAsync(user1Id, "tablet-001");

    var conversationId = await CreatePrivateConversationAsync(user1Id, user2Id);

    var message = await SendMessageAsync(
        conversationId,
        user1Id,
        Guid.NewGuid().ToString(),
        "Original message");

    var editResponse = await _client.PutAsJsonAsync(
        $"/api/v1/messages/{message.Id}",
        new EditMessageRequest(user1Id, "Edited message"));

    editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var syncEvents = await GetSyncEventsAsync(user1Id, "tablet-001", 0);

    syncEvents.Events
        .Should()
        .Contain(x => x.EventType == "MessageEdited" && x.MessageId == message.Id);
}


[Fact]
public async Task DeleteMessage_Should_Create_MessageDeleted_SyncEvent()
{
    var user1Id = await CreateUserAsync("User 1");
    var user2Id = await CreateUserAsync("User 2");

    await RegisterDeviceAsync(user1Id, "tablet-001");

    var conversationId = await CreatePrivateConversationAsync(user1Id, user2Id);

    var message = await SendMessageAsync(
        conversationId,
        user1Id,
        Guid.NewGuid().ToString(),
        "Message to delete");

    var deleteResponse = await _client.DeleteAsync(
        $"/api/v1/messages/{message.Id}?userId={user1Id}");

    deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var syncEvents = await GetSyncEventsAsync(user1Id, "tablet-001", 0);

    syncEvents.Events
        .Should()
        .Contain(x => x.EventType == "MessageDeleted" && x.MessageId == message.Id);
}


private async Task RegisterDeviceAsync(Guid userId, string deviceId)
{
    var request = new RegisterDeviceRequest(
        deviceId,
        "Test Device");

    var response = await _client.PostAsJsonAsync(
        $"/api/v1/users/{userId}/devices",
        request);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

private async Task<SyncEventsResponse> GetSyncEventsAsync(
    Guid userId,
    string deviceId,
    long afterEventId)
{
    var response = await _client.GetAsync(
        $"/api/v1/sync/{userId}/{deviceId}?afterEventId={afterEventId}&take=100");

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await response.Content.ReadFromJsonAsync<SyncEventsResponse>();
    result.Should().NotBeNull();

    return result!;
}


    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var request = new CreateUserRequest(
            displayName,
            $"user-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/v1/users", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CreateUserResponse>();
        result.Should().NotBeNull();

        return result!.Id;
    }

    private async Task<Guid> CreatePrivateConversationAsync(Guid firstUserId, Guid secondUserId)
    {
        var request = new CreatePrivateConversationRequest(
            firstUserId,
            secondUserId);

        var response = await _client.PostAsJsonAsync("/api/v1/conversations/private", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CreateConversationResponse>();
        result.Should().NotBeNull();

        return result!.Id;
    }

    private async Task<SendMessageResponse> SendMessageAsync(
        Guid conversationId,
        Guid senderUserId,
        string clientMessageId,
        string body)
    {
        var request = new SendMessageRequest(
            conversationId,
            senderUserId,
            Guid.Parse(clientMessageId),
            body,
            null,
            null);

        var response = await _client.PostAsJsonAsync("/api/v1/messages", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        result.Should().NotBeNull();

        return result!;
    }

    private sealed record CreateUserResponse(
        Guid Id,
        string DisplayName,
        string UserName);

    private sealed record CreateConversationResponse(
        Guid Id);

  private sealed record SendMessageResponse(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    bool IsDuplicate);



private sealed record SyncEventResponse(
    long EventId,
    string EventType,
    Guid? ConversationId,
    Guid? MessageId,
    string PayloadJson,
    DateTime CreatedAtUtc);

private sealed record SyncEventsResponse(
    Guid UserId,
    string DeviceId,
    long AfterEventId,
    long LatestEventId,
    bool HasMore,
    List<SyncEventResponse> Events);

}


