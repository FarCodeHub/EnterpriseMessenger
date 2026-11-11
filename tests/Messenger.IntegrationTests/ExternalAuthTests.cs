using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Application.Auth.External;
using Messenger.Domain.Users;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

public sealed class ExternalAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ExternalAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthConfig_Should_Allow_Local_Login_In_Testing_Without_Keycloak()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<AuthConfigResponse>();
        config!.LocalLoginEnabled.Should().BeTrue();
        config.Keycloak.Should().BeNull();
    }

    [Fact]
    public async Task Provision_Should_Link_Existing_User_With_Same_UserName()
    {
        var userName = $"ali-{Guid.NewGuid():N}";
        Guid existingUserId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
            var user = new User("Ali", userName);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            existingUserId = user.Id;
        }

        var subject = Guid.NewGuid().ToString();

        var result = await ProvisionAsync(subject, userName.ToUpperInvariant(), "Ali Rezaei");

        result.UserId.Should().Be(existingUserId);
        result.DisplayName.Should().Be("Ali Rezaei");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MessengerDbContext>();
        var stored = await verifyDb.Users.SingleAsync(x => x.Id == existingUserId);
        stored.ExternalSubject.Should().Be(subject);
    }

    [Fact]
    public async Task Provision_Should_Create_User_Once_And_Return_It_On_Next_Sign_In()
    {
        var subject = Guid.NewGuid().ToString();
        var userName = $"sara-{Guid.NewGuid():N}";

        var first = await ProvisionAsync(subject, userName, "Sara");
        var second = await ProvisionAsync(subject, userName, "Sara");

        second.UserId.Should().Be(first.UserId);
        first.IsActive.Should().BeTrue();
        first.IsAdmin.Should().BeFalse();
    }

    private async Task<ExternalUserResult> ProvisionAsync(string subject, string userName, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<IExternalUserProvisioner>();

        return await provisioner.ProvisionAsync(
            new ExternalUserCommand(subject, userName, displayName),
            CancellationToken.None);
    }

    private sealed record AuthConfigResponse(bool LocalLoginEnabled, object? Keycloak);
}
