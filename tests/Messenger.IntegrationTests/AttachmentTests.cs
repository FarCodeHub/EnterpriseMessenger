using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;
using Messenger.Domain.Attachments;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

public sealed class AttachmentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AttachmentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_Should_Use_Stored_Metadata_For_Own_Upload()
    {
        var senderId = await CreateUserAsync("Sender");
        var receiverId = await CreateUserAsync("Receiver");
        var conversationId = await CreatePrivateConversationAsync(senderId, receiverId);
        var fileId = await SeedUploadedFileAsync(senderId, "attachments/2026/01/01/real.pdf");

        SetCurrentUser(senderId);

        // The client-supplied storage path must be ignored.
        var response = await SendWithAttachmentAsync(
            conversationId,
            new SendMessageAttachmentRequest(
                FileId: fileId,
                FileName: "spoofed.html",
                ContentType: "text/html",
                Size: 1,
                StoragePath: "attachments/someone-else/secret.pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var attachment = await dbContext.Attachments
            .Join(dbContext.Messages.Where(x => x.ConversationId == conversationId),
                a => a.MessageId,
                m => m.Id,
                (a, _) => a)
            .SingleAsync();

        attachment.StoragePath.Should().Be("attachments/2026/01/01/real.pdf");
        attachment.FileName.Should().Be("real.pdf");
        attachment.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task SendMessage_Should_Reject_File_Uploaded_By_Another_User()
    {
        var senderId = await CreateUserAsync("Sender");
        var receiverId = await CreateUserAsync("Receiver");
        var otherUserId = await CreateUserAsync("Other");
        var conversationId = await CreatePrivateConversationAsync(senderId, receiverId);
        var otherUsersFileId = await SeedUploadedFileAsync(otherUserId, "attachments/2026/01/01/other.pdf");

        SetCurrentUser(senderId);

        var response = await SendWithAttachmentAsync(
            conversationId,
            new SendMessageAttachmentRequest(FileId: otherUsersFileId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_Should_Reject_Attachment_Without_FileId()
    {
        var senderId = await CreateUserAsync("Sender");
        var receiverId = await CreateUserAsync("Receiver");
        var conversationId = await CreatePrivateConversationAsync(senderId, receiverId);

        SetCurrentUser(senderId);

        var response = await SendWithAttachmentAsync(
            conversationId,
            new SendMessageAttachmentRequest(StoragePath: "attachments/2026/01/01/guess.pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> SeedUploadedFileAsync(Guid uploaderUserId, string storagePath)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();

        var file = new UploadedFile(
            Guid.NewGuid(),
            uploaderUserId,
            Path.GetFileName(storagePath),
            "application/pdf",
            1024,
            storagePath);

        dbContext.UploadedFiles.Add(file);
        await dbContext.SaveChangesAsync();

        return file.Id;
    }

    private Task<HttpResponseMessage> SendWithAttachmentAsync(
        Guid conversationId,
        SendMessageAttachmentRequest attachment)
    {
        return _client.PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageRequest(
                conversationId,
                Guid.NewGuid(),
                string.Empty,
                null,
                [attachment]));
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

        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        return result!.Id;
    }

    private async Task<Guid> CreatePrivateConversationAsync(Guid firstUserId, Guid secondUserId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/conversations/private",
            new CreatePrivateConversationRequest(firstUserId, secondUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        return result!.Id;
    }

    private sealed record IdResponse(Guid Id);
}
