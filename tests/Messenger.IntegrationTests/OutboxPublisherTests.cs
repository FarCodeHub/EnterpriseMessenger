using System.Text.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Messenger.Application.Outbox;
using Messenger.Contracts.IntegrationEvents;
using Messenger.Domain.Outbox;
using Messenger.Infrastructure.Outbox;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.IntegrationTests;

/// <summary>
/// The outbox publisher on an in-memory bus. Until Nov 2026 the worker published at most 50 events every 5 s
/// (10/s); under load (80 messages/s) search indexing and notifications fell minutes behind.
/// </summary>
public sealed class OutboxPublisherTests
{
    [Fact]
    public async Task Publishes_A_Backlog_In_Full_Batches_And_Marks_Every_Event_Processed()
    {
        await using var outbox = await OutboxUnderTest.CreateAsync(UseInMemory);
        await outbox.AddEventsAsync(450);

        var batches = new List<int>();

        for (var taken = -1; taken != 0;)
        {
            taken = await outbox.PublishBatchAsync(200);
            batches.Add(taken);
        }

        batches.Should().Equal(200, 200, 50, 0);
        (await outbox.PublishedIdsAsync(450)).Should().HaveCount(450).And.OnlyHaveUniqueItems();
        (await outbox.PendingAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_Broken_Event_Is_Retried_Later_Without_Blocking_The_Others()
    {
        await using var outbox = await OutboxUnderTest.CreateAsync(UseInMemory);
        await outbox.AddEventsAsync(3);
        await outbox.AddAsync(new OutboxMessage("No.Such.Type, Nowhere", "{}"));

        (await outbox.PublishBatchAsync(10)).Should().Be(4);

        (await outbox.PublishedIdsAsync(3)).Should().HaveCount(3);

        await using var scope = outbox.CreateScope();
        var broken = await scope.ServiceProvider.GetRequiredService<MessengerDbContext>()
            .OutboxMessages.SingleAsync(x => x.Type.StartsWith("No.Such.Type"));

        broken.ProcessedAtUtc.Should().BeNull();
        broken.RetryCount.Should().Be(1);
        broken.Error.Should().Contain("Cannot resolve");
    }

    [PostgresFact]
    public async Task Two_Instances_Publish_Each_Event_Exactly_Once()
    {
        var database = $"messenger_outbox_{Guid.NewGuid():N}";
        var connectionString =
            $"{Environment.GetEnvironmentVariable(PostgresWebApplicationFactory.ConnectionStringVariable)};Database={database}";

        await using var outbox = await OutboxUnderTest.CreateAsync(
            options => options.UseNpgsql(connectionString),
            migrate: true);

        try
        {
            await outbox.AddEventsAsync(1_000);

            // Two API instances draining the same outbox at the same time.
            async Task<int> DrainAsync()
            {
                var total = 0;

                for (int taken; (taken = await outbox.PublishBatchAsync(50)) > 0;)
                    total += taken;

                return total;
            }

            var drained = await Task.WhenAll(DrainAsync(), DrainAsync());

            drained.Sum().Should().Be(1_000, "every event is taken by exactly one instance");
            drained.Should().OnlyContain(x => x > 0, "both instances did work");
            (await outbox.PublishedIdsAsync(1_000)).Should().HaveCount(1_000).And.OnlyHaveUniqueItems();
            (await outbox.PendingAsync()).Should().Be(0);
        }
        finally
        {
            await using var scope = outbox.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MessengerDbContext>().Database.EnsureDeletedAsync();
        }
    }

    private static readonly Action<DbContextOptionsBuilder> UseInMemory =
        CreateInMemory($"outbox-{Guid.NewGuid()}");

    private static Action<DbContextOptionsBuilder> CreateInMemory(string name) =>
        options => options
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));

    private sealed class OutboxUnderTest : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly ITestHarness _harness;

        private OutboxUnderTest(ServiceProvider provider)
        {
            _provider = provider;
            _harness = provider.GetRequiredService<ITestHarness>();
        }

        public static async Task<OutboxUnderTest> CreateAsync(
            Action<DbContextOptionsBuilder> database,
            bool migrate = false)
        {
            // Each test gets its own in-memory database.
            if (ReferenceEquals(database, UseInMemory))
                database = CreateInMemory($"outbox-{Guid.NewGuid()}");

            var provider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                .AddDbContext<MessengerDbContext>(database)
                .AddScoped<IOutboxPublisher, OutboxPublisher>()
                .AddMassTransitTestHarness()
                .BuildServiceProvider();

            var outbox = new OutboxUnderTest(provider);

            if (migrate)
            {
                await using var scope = outbox.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MessengerDbContext>().Database.MigrateAsync();
            }

            await outbox._harness.Start();
            return outbox;
        }

        public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

        public async Task AddEventsAsync(int count)
        {
            var type = typeof(TestIntegrationEvent).AssemblyQualifiedName!;

            for (var i = 0; i < count; i++)
                await AddAsync(new OutboxMessage(type, JsonSerializer.Serialize(new TestIntegrationEvent(Guid.NewGuid(), $"event {i}", DateTime.UtcNow))));
        }

        public async Task AddAsync(OutboxMessage message)
        {
            await using var scope = CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerDbContext>();
            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();
        }

        public async Task<int> PublishBatchAsync(int batchSize)
        {
            await using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IOutboxPublisher>()
                .PublishBatchAsync(batchSize, CancellationToken.None);
        }

        /// <summary>The published events' texts, once at least <paramref name="expected"/> have been recorded.</summary>
        public async Task<List<string>> PublishedIdsAsync(int expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            List<string> published;

            do
            {
                published = _harness.Published.Select<TestIntegrationEvent>()
                    .Select(x => x.Context.Message.Message)
                    .ToList();

                if (published.Count >= expected)
                    break;

                await Task.Delay(20);
            }
            while (DateTime.UtcNow < deadline);

            return published;
        }

        public async Task<int> PendingAsync()
        {
            await using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<MessengerDbContext>()
                .OutboxMessages.CountAsync(x => x.ProcessedAtUtc == null);
        }

        public async ValueTask DisposeAsync()
        {
            await _harness.Stop();
            await _provider.DisposeAsync();
        }
    }
}
