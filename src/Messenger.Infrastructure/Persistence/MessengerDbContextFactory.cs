using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Messenger.Infrastructure.Persistence;

public sealed class MessengerDbContextFactory
    : IDesignTimeDbContextFactory<MessengerDbContext>
{
    public MessengerDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        var basePath = currentDirectory.EndsWith("Messenger.Api", StringComparison.OrdinalIgnoreCase)
            ? currentDirectory
            : Path.Combine(currentDirectory, "src", "Messenger.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("MessengerDb");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'MessengerDb' was not found.");

        var optionsBuilder = new DbContextOptionsBuilder<MessengerDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new MessengerDbContext(optionsBuilder.Options);
    }
}