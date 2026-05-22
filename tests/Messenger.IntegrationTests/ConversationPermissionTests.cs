using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Users;

namespace Messenger.IntegrationTests;

public sealed class ConversationPermissionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConversationPermissionTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private void SetCurrentUser(Guid userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }
private async Task<Guid> CreateUserAsync(string name)
{
    SetCurrentUser(Guid.NewGuid());

    var response = await _client.PostAsJsonAsync(
        "/api/v1/users",
        new CreateUserRequest(
            name,
            $"user-{Guid.NewGuid():N}"));

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await response.Content.ReadFromJsonAsync<CreateUserResponse>();

    return result!.Id;
}

private async Task<Guid> CreateGroupAsync(Guid ownerId, params Guid[] memberIds)
{
    SetCurrentUser(ownerId);

    var response = await _client.PostAsJsonAsync(
        "/api/v1/conversations/group",
        new CreateGroupConversationRequest(
            "Test Group",
            ownerId,
            memberIds.ToList()));

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var result = await response.Content.ReadFromJsonAsync<CreateConversationResponse>();

    return result!.Id;
}

private sealed record CreateUserResponse(
    Guid Id,
    string DisplayName,
    string UserName);

private sealed record CreateConversationResponse(
    Guid Id);

    [Fact]
public async Task Member_Should_Not_Add_New_Member()
{
    var ownerId = await CreateUserAsync("Owner");
    var memberId = await CreateUserAsync("Member");
    var newUserId = await CreateUserAsync("New User");

    var conversationId = await CreateGroupAsync(ownerId, memberId);

    SetCurrentUser(memberId);

    var response = await _client.PostAsJsonAsync(
        $"/api/v1/conversations/{conversationId}/members",
        new AddConversationMemberRequest(newUserId));

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}


[Fact]
public async Task Owner_Should_Promote_Member_To_Admin()
{
    var ownerId = await CreateUserAsync("Owner");
    var memberId = await CreateUserAsync("Member");

    var conversationId = await CreateGroupAsync(ownerId, memberId);

    SetCurrentUser(ownerId);

    var response = await _client.PutAsJsonAsync(
        $"/api/v1/conversations/{conversationId}/members/{memberId}/role",
        new SetConversationMemberRoleRequest(2));

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var body = await response.Content.ReadAsStringAsync();

    body.Should().Contain("Admin");
}
 [Fact]
public async Task Admin_Should_Add_New_Member()
{
    var ownerId = await CreateUserAsync("Owner");
    var adminId = await CreateUserAsync("Admin");
    var newUserId = await CreateUserAsync("New User");

    var conversationId = await CreateGroupAsync(ownerId, adminId);

    SetCurrentUser(ownerId);

    var promoteResponse = await _client.PutAsJsonAsync(
        $"/api/v1/conversations/{conversationId}/members/{adminId}/role",
        new SetConversationMemberRoleRequest(2));

    promoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    SetCurrentUser(adminId);

    var addResponse = await _client.PostAsJsonAsync(
        $"/api/v1/conversations/{conversationId}/members",
        new AddConversationMemberRequest(newUserId));

    addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
}
[Fact]
public async Task Admin_Should_Not_Remove_Another_Admin()
{
    var ownerId = await CreateUserAsync("Owner");

    var admin1Id = await CreateUserAsync("Admin1");
    var admin2Id = await CreateUserAsync("Admin2");

    var conversationId = await CreateGroupAsync(
        ownerId,
        admin1Id,
        admin2Id);

    SetCurrentUser(ownerId);

    var promoteAdmin1 = await _client.PutAsJsonAsync(
        $"/api/v1/conversations/{conversationId}/members/{admin1Id}/role",
        new SetConversationMemberRoleRequest(2));

    promoteAdmin1.StatusCode.Should().Be(HttpStatusCode.OK);

    var promoteAdmin2 = await _client.PutAsJsonAsync(
        $"/api/v1/conversations/{conversationId}/members/{admin2Id}/role",
        new SetConversationMemberRoleRequest(2));

    promoteAdmin2.StatusCode.Should().Be(HttpStatusCode.OK);

    SetCurrentUser(admin1Id);

    var removeResponse = await _client.DeleteAsync(
        $"/api/v1/conversations/{conversationId}/members/{admin2Id}");

    removeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Owner_Should_Not_Remove_Himself()
{
    var ownerId = await CreateUserAsync("Owner");
    var memberId = await CreateUserAsync("Member");

    var conversationId = await CreateGroupAsync(ownerId, memberId);

    SetCurrentUser(ownerId);

    var response = await _client.DeleteAsync(
        $"/api/v1/conversations/{conversationId}/members/{ownerId}");

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

}