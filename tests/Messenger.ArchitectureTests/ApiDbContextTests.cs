using FluentAssertions;
using NetArchTest.Rules;

namespace Messenger.ArchitectureTests;

/// <summary>
/// The Api layer should go through Application interfaces, not query <c>MessengerDbContext</c> directly.
/// The types below still do (known debt). The list may only shrink: a new type that uses the DbContext fails
/// the test, and so does a listed type that no longer needs to be listed.
/// </summary>
public sealed class ApiDbContextTests
{
    private const string DbContextType = "Messenger.Infrastructure.Persistence.MessengerDbContext";

    private static readonly string[] KnownDirectDbContextUsers =
    [
        // Read-only admin and search queries.
        "Messenger.Api.Controllers.AuditLogsController",
        "Messenger.Api.Controllers.DeadLetterOutboxController",
        "Messenger.Api.Controllers.SearchController",

        // Outbox and clean-up jobs: they belong in Infrastructure.
        "Messenger.Api.BackgroundServices.AuditLogCleanupWorker",
        "Messenger.Api.BackgroundServices.DeadLetterOutboxMoverWorker",
        "Messenger.Api.BackgroundServices.NotificationCleanupWorker",
        "Messenger.Api.BackgroundServices.NotificationDispatchWorker",
        "Messenger.Api.BackgroundServices.OutboxCleanupWorker",
        "Messenger.Api.BackgroundServices.RefreshTokenCleanupWorker",
    ];

    [Fact]
    public void Only_Known_Api_Types_Use_The_DbContext_Directly()
    {
        var result = Types
            .InAssembly(typeof(Messenger.Api.Hubs.ChatHub).Assembly)
            .ShouldNot()
            .HaveDependencyOn(DbContextType)
            .GetResult();

        // Async methods and lambdas compile to nested types (Outer+<Method>d__1); report the declaring type.
        var actual = (result.FailingTypeNames ?? [])
            .Select(name => name.Split('+')[0])
            .Distinct()
            .Order()
            .ToList();

        var added = actual.Except(KnownDirectDbContextUsers).ToList();
        var fixedTypes = KnownDirectDbContextUsers.Except(actual).ToList();

        added.Should().BeEmpty(
            "Api types should use Application interfaces instead of MessengerDbContext. New direct users: {0}",
            string.Join(", ", added));

        fixedTypes.Should().BeEmpty(
            "these types no longer use MessengerDbContext; remove them from KnownDirectDbContextUsers: {0}",
            string.Join(", ", fixedTypes));
    }
}
