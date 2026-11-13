using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;
using Messenger.Domain.Attachments;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

/// <summary>
/// What forwarding stores and returns: the new messages, their attachments, deliveries, sync events,
/// audit entries, outbox events and the target conversation's last message.
/// </summary>
public sealed class MessageForwardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MessageForwardTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Forward_Creates_A_Message_In_Every_Target_With_Its_Side_Effects()
    {
        var s = await CreateScenarioAsync();
        var source = await SendAsync(s.Bob, s.SourceConversationId, "quarterly report");
        await AddAttachmentAsync(source.Id, "report.pdf", "attachments/report.pdf");
        var groupBefore = await SendAsync(s.Alice, s.GroupId, "earlier message");

        SetCurrentUser(s.Alice);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/messages/forward",
            new ForwardMessageRequest(source.Id, [s.GroupId, s.PrivateWithCarolId], null));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var results = (await response.Content.ReadFromJsonAsync<List<ForwardResult>>())!;
        results.Select(x => x.ConversationId).Should().BeEquivalentTo([s.GroupId, s.PrivateWithCarolId]);

        foreach (var result in results)
        {
            result.SenderUserId.Should().Be(s.Alice);
            result.Body.Should().Be("quarterly report");
            result.IsDuplicate.Should().BeFalse();
            result.ForwardedFromMessageId.Should().Be(source.Id);
            result.ForwardedFromUserId.Should().Be(s.Bob);
            result.ForwardedFromDisplayName.Should().Be("Bob");
            result.Attachments.Should().ContainSingle()
                .Which.Should().Match<AttachmentResult>(x =>
                    x.FileName == "report.pdf" && x.StoragePath == "attachments/report.pdf");
        }

        var inGroup = results.Single(x => x.ConversationId == s.GroupId);
        inGroup.SequenceNumber.Should().Be(groupBefore.SequenceNumber + 1);

        await AssertStoredSideEffectsAsync(inGroup, expectedRecipients: [s.Alice, s.Carol], sourceMessageId: source.Id);
    }

    [Fact]
    public async Task Forward_Batch_Keeps_Source_Order_And_Numbers_Consecutively()
    {
        var s = await CreateScenarioAsync();
        var first = await SendAsync(s.Bob, s.SourceConversationId, "first");
        var second = await SendAsync(s.Alice, s.SourceConversationId, "second");
        var groupBefore = await SendAsync(s.Alice, s.GroupId, "earlier message");

        SetCurrentUser(s.Alice);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/messages/forward-batch",
            new ForwardMessagesRequest([second.Id, first.Id], [s.GroupId, s.PrivateWithCarolId], null));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var results = (await response.Content.ReadFromJsonAsync<List<ForwardResult>>())!;
        results.Should().HaveCount(4);

        var inGroup = results.Where(x => x.ConversationId == s.GroupId).OrderBy(x => x.SequenceNumber).ToList();
        inGroup.Select(x => x.SequenceNumber)
            .Should().Equal(groupBefore.SequenceNumber + 1, groupBefore.SequenceNumber + 2);
        inGroup.Select(x => x.Body).Should().Equal("first", "second");
        inGroup.Select(x => x.ForwardedFromUserId).Should().Equal(s.Bob, s.Alice);
        inGroup.Select(x => x.ForwardedFromDisplayName).Should().Equal("Bob", "Alice");

        await AssertStoredSideEffectsAsync(inGroup[1], expectedRecipients: [s.Alice, s.Carol], sourceMessageId: second.Id);
    }

    [Fact]
    public async Task Forward_Rejects_Deleted_Source()
    {
        var s = await CreateScenarioAsync();
        var source = await SendAsync(s.Bob, s.SourceConversationId, "to delete");

        SetCurrentUser(s.Bob);
        (await _client.DeleteAsync($"/api/v1/messages/{source.Id}?userId={s.Bob}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        SetCurrentUser(s.Alice);
        var single = await _client.PostAsJsonAsync(
            "/api/v1/messages/forward", new ForwardMessageRequest(source.Id, [s.GroupId], null));
        var batch = await _client.PostAsJsonAsync(
            "/api/v1/messages/forward-batch", new ForwardMessagesRequest([source.Id], [s.GroupId], null));

        single.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        batch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Forward_Requires_Membership_Of_Source_And_Every_Target()
    {
        var s = await CreateScenarioAsync();
        var source = await SendAsync(s.Bob, s.SourceConversationId, "private");

        // Carol is not in the source conversation.
        SetCurrentUser(s.Carol);
        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward", new ForwardMessageRequest(source.Id, [s.GroupId], null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward-batch", new ForwardMessagesRequest([source.Id], [s.GroupId], null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Bob is not in the group.
        SetCurrentUser(s.Bob);
        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward", new ForwardMessageRequest(source.Id, [s.GroupId], null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward-batch", new ForwardMessagesRequest([source.Id], [s.GroupId], null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
        (await dbContext.Messages.AnyAsync(x => x.ForwardedFromMessageId == source.Id))
            .Should().BeFalse("a rejected forward must not store anything");
    }

    [Fact]
    public async Task Forward_Batch_Rejects_Invalid_Selections()
    {
        var s = await CreateScenarioAsync();
        var fromSource = await SendAsync(s.Alice, s.SourceConversationId, "one");
        var fromGroup = await SendAsync(s.Alice, s.GroupId, "two");

        SetCurrentUser(s.Alice);

        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward-batch",
                new ForwardMessagesRequest([fromSource.Id, fromGroup.Id], [s.PrivateWithCarolId], null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "sources from two conversations are not supported");

        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward-batch",
                new ForwardMessagesRequest([], [s.PrivateWithCarolId], null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward-batch",
                new ForwardMessagesRequest([fromSource.Id], [], null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.PostAsJsonAsync(
                "/api/v1/messages/forward-batch",
                new ForwardMessagesRequest([Guid.NewGuid()], [s.PrivateWithCarolId], null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task AssertStoredSideEffectsAsync(ForwardResult forwarded, Guid[] expectedRecipients, Guid sourceMessageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var message = await dbContext.Messages.SingleAsync(x => x.Id == forwarded.Id);
        message.ForwardedFromMessageId.Should().Be(sourceMessageId);
        message.SequenceNumber.Should().Be(forwarded.SequenceNumber);

        (await dbContext.MessageDeliveries.Where(x => x.MessageId == forwarded.Id).Select(x => x.UserId).ToListAsync())
            .Should().BeEquivalentTo(expectedRecipients, "members who left get no deliveries");

        var syncEvents = await dbContext.UserSyncEvents.Where(x => x.MessageId == forwarded.Id).ToListAsync();
        syncEvents.Select(x => x.UserId).Should().BeEquivalentTo(expectedRecipients);
        syncEvents.Should().OnlyContain(x => x.EventType == "MessageCreated");
        using (var payload = JsonDocument.Parse(syncEvents[0].PayloadJson))
        {
            payload.RootElement.GetProperty("ForwardedFromMessageId").GetGuid().Should().Be(sourceMessageId);
            payload.RootElement.GetProperty("SequenceNumber").GetInt64().Should().Be(forwarded.SequenceNumber);
        }

        (await dbContext.AuditLogs.CountAsync(x => x.EntityId == forwarded.Id.ToString() && x.EventType == "MessageForwarded"))
            .Should().Be(1);

        var outbox = (await dbContext.OutboxMessages.ToListAsync())
            .Single(x => x.Payload.Contains(forwarded.Id.ToString()));
        using (var payload = JsonDocument.Parse(outbox.Payload))
        {
            payload.RootElement.GetProperty("ForwardedFromMessageId").GetGuid().Should().Be(sourceMessageId);
            payload.RootElement.GetProperty("SequenceNumber").GetInt64().Should().Be(forwarded.SequenceNumber);
        }

        (await dbContext.Conversations.SingleAsync(x => x.Id == forwarded.ConversationId))
            .LastMessageId.Should().NotBeNull();

        (await dbContext.Attachments.Where(x => x.MessageId == forwarded.Id).Select(x => x.StoragePath).ToListAsync())
            .Should().BeEquivalentTo(forwarded.Attachments.Select(x => x.StoragePath));
    }

    /// <summary>
    /// Alice and Bob share the source conversation. Alice, Carol and Dave are in a group, and Dave has left it.
    /// Alice and Carol also have a private conversation.
    /// </summary>
    private async Task<Scenario> CreateScenarioAsync()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var dave = await CreateUserAsync("Dave");

        var sourceConversationId = await CreatePrivateConversationAsync(alice, bob);
        var privateWithCarolId = await CreatePrivateConversationAsync(alice, carol);

        var groupResponse = await _client.PostAsJsonAsync(
            "/api/v1/conversations/group",
            new CreateGroupConversationRequest("Team", alice, [carol, dave]));
        groupResponse.StatusCode.Should().Be(HttpStatusCode.OK, await groupResponse.Content.ReadAsStringAsync());
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        SetCurrentUser(dave);
        (await _client.PostAsync($"/api/v1/conversations/{groupId}/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return new Scenario(alice, bob, carol, sourceConversationId, groupId, privateWithCarolId);
    }

    private async Task<SentMessage> SendAsync(Guid senderId, Guid conversationId, string body)
    {
        SetCurrentUser(senderId);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(conversationId, Guid.NewGuid(), body));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<SentMessage>())!;
    }

    private async Task AddAttachmentAsync(Guid messageId, string fileName, string storagePath)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
        dbContext.Attachments.Add(new Attachment(messageId, fileName, "application/pdf", 1234, storagePath));
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreatePrivateConversationAsync(Guid firstUserId, Guid secondUserId)
    {
        SetCurrentUser(firstUserId);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(firstUserId, secondUserId));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private void SetCurrentUser(Guid userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }

    private sealed record Scenario(
        Guid Alice, Guid Bob, Guid Carol, Guid SourceConversationId, Guid GroupId, Guid PrivateWithCarolId);

    private sealed record IdResponse(Guid Id);

    private sealed record SentMessage(Guid Id, long SequenceNumber);

    private sealed record AttachmentResult(Guid Id, string FileName, string ContentType, long Size, string StoragePath);

    private sealed record ForwardResult(
        Guid Id,
        Guid ConversationId,
        long SequenceNumber,
        Guid SenderUserId,
        string Body,
        bool IsDuplicate,
        Guid? ForwardedFromMessageId,
        Guid? ForwardedFromUserId,
        string? ForwardedFromDisplayName,
        List<AttachmentResult> Attachments);
}
