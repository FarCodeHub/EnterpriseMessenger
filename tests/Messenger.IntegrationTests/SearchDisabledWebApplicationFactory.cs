using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Messenger.IntegrationTests;

/// <summary>
/// The app as configured without OpenSearch (<c>OpenSearch:Url</c> empty) and with the real search
/// registrations, not fakes. The service provider validates every registration at startup, so a service
/// that needs something only registered when OpenSearch is configured fails here instead of on the first request.
/// </summary>
public sealed class SearchDisabledWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool UseFakeSearch => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        DisableOpenSearch(builder);
    }

    public static void DisableOpenSearch(IWebHostBuilder builder)
    {
        builder.UseSetting("OpenSearch:Url", "");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenSearch:Url"] = "" }));

        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
    }
}
