using Messenger.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;

namespace Messenger.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MessengerTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<MessengerDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("MessengerDbContext") == true)
                .ToList();


                var hostedServices = services
    .Where(d => d.ServiceType == typeof(IHostedService))
    .ToList();

foreach (var hostedService in hostedServices)
{
    services.Remove(hostedService);
}

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<MessengerDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                });
            });

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