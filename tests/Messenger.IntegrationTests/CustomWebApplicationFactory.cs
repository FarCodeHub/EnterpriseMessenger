using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Messenger.Application.Abstractions.Realtime;
using Messenger.IntegrationTests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using OpenSearch.Client;
using Messenger.Application.Search;
namespace Messenger.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MessengerTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:IsEnabled"] = "true",
                ["Redis:ConnectionString"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<IPresenceService>();
            services.RemoveAll<IOpenSearchClient>();
services.RemoveAll<IMessageSearchIndexer>();
services.AddSingleton<IMessageSearchIndexer, FakeMessageSearchIndexer>();
services.RemoveAll<IMessageFullTextSearchService>();
services.AddSingleton<IMessageFullTextSearchService, FakeMessageFullTextSearchService>();

            var dbContextDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<MessengerDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions))
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
                services.Remove(descriptor);

            services.AddDbContext<MessengerDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                });
            });

            services.AddSingleton<IPresenceService, FakePresenceService>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { });
        });
    }
}