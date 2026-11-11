using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Messenger.IntegrationTests;

/// <summary>
/// Runs the API against a real PostgreSQL database (created from the migrations), for behaviour the
/// in-memory provider cannot show: row locks, unique indexes and concurrent requests.
/// Set MESSENGER_TEST_POSTGRES to a server connection string to enable these tests; CI does.
/// </summary>
public sealed class PostgresWebApplicationFactory : CustomWebApplicationFactory
{
    public const string ConnectionStringVariable = "MESSENGER_TEST_POSTGRES";

    private readonly string _connectionString =
        $"{Environment.GetEnvironmentVariable(ConnectionStringVariable)};Database=messenger_test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MessengerDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<MessengerDbContext>>();

            services.AddDbContext<MessengerDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MessengerDbContext>().Database.Migrate();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<MessengerDbContext>().Database.EnsureDeleted();
            }
            catch
            {
                // Best effort clean-up of the throw-away database.
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>A test that runs only when a PostgreSQL server is configured for tests.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresWebApplicationFactory.ConnectionStringVariable)))
            Skip = $"Set {PostgresWebApplicationFactory.ConnectionStringVariable} to run PostgreSQL tests.";
    }
}
