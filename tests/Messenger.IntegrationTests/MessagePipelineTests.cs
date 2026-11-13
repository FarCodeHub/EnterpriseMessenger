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

public sealed class MessagePipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MessagePipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Member_Who_Left_Should_Not_Receive_New_Messages()
    {
        var owner = await CreateUserAsync("Owner");
        var stays = await CreateUserAsync("Stays");
        var leaves = await CreateUserAsync("Leaves");

        var conversationId = await CreateGroupAsync(owner, [stays, leaves]);

        SetCurrentUser(leaves);
        var leaveResponse = await _client.PostAsync($"/api/v1/conversations/{conversationId}/leave", null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.OK, await leaveResponse.Content.ReadAsStringAsync());

        SetCurrentUser(owner);
        var sendResponse = await _client.PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), "After leaving"));
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK, await sendResponse.Content.ReadAsStringAsync());

        var sent = await sendResponse.Content.ReadFromJsonAsync<IdResponse>();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var deliveredTo = await dbContext.MessageDeliveries
            .Where(x => x.MessageId == sent!.Id)
            .Select(x => x.UserId)
            .ToListAsync();

        deliveredTo.Should().BeEquivalentTo([owner, stays]);

        var syncedTo = await dbContext.UserSyncEvents
            .Where(x => x.MessageId == sent!.Id)
            .Select(x => x.UserId)
            .ToListAsync();

        syncedTo.Should().BeEquivalentTo([owner, stays]);
    }

    [Fact]
    public async Task Messages_In_One_Conversation_Should_Get_Consecutive_Sequence_Numbers()
    {
        var first = await CreateUserAsync("First");
        var second = await CreateUserAsync("Second");
        var conversationId = await CreatePrivateConversationAsync(first, second);

        SetCurrentUser(first);

        var sequenceNumbers = new List<long>();

        for (var i = 0; i < 5; i++)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/messages",
                new SendMessageRequest(conversationId, Guid.NewGuid(), $"Message {i}"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<SequenceResponse>();
            sequenceNumbers.Add(result!.SequenceNumber);
        }

        sequenceNumbers.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Creating_A_Private_Conversation_For_Other_Users_Should_Be_Forbidden()
    {
        var first = await CreateUserAsync("First");
        var second = await CreateUserAsync("Second");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "User");

        var response = await client.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(first, second));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void SetCurrentUser(Guid userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreateGroupAsync(Guid createdBy, List<Guid> members)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/conversations/group",
            new CreateGroupConversationRequest("Team", createdBy, members));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreatePrivateConversationAsync(Guid firstUserId, Guid secondUserId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(firstUserId, secondUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record SequenceResponse(Guid Id, long SequenceNumber);
}
