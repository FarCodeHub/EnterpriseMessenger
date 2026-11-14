using System.Reflection;
using Messenger.Infrastructure.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Messenger.Api.Observability;

/// <summary>
/// Sends traces and metrics over OTLP when their endpoints are configured; does nothing otherwise.
/// <list type="bullet">
/// <item><c>OpenTelemetry:TracesEndpoint</c>, e.g. <c>http://jaeger:4318/v1/traces</c></item>
/// <item><c>OpenTelemetry:MetricsEndpoint</c>, e.g. <c>http://prometheus:9090/api/v1/otlp/v1/metrics</c>
/// (Prometheus 3 with <c>--web.enable-otlp-receiver</c>)</item>
/// </list>
/// </summary>
public static class ObservabilitySetup
{
    public const string ServiceName = "messenger-api";

    // Meters and sources that ASP.NET Core, SignalR, Kestrel, EF Core, Npgsql and MassTransit publish themselves.
    private static readonly string[] Meters =
    [
        MessengerMetrics.MeterName,
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Http.Connections",
        "Microsoft.AspNetCore.SignalR.Server",
        "Microsoft.AspNetCore.RateLimiting",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "MassTransit",
    ];

    private static readonly string[] TraceSources = ["Npgsql", "MassTransit"];

    public static IServiceCollection AddMessengerObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var tracesEndpoint = configuration["OpenTelemetry:TracesEndpoint"];
        var metricsEndpoint = configuration["OpenTelemetry:MetricsEndpoint"];

        if (string.IsNullOrWhiteSpace(tracesEndpoint) && string.IsNullOrWhiteSpace(metricsEndpoint))
            return services;

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                ServiceName,
                serviceVersion: version,
                serviceInstanceId: Environment.MachineName));

        if (!string.IsNullOrWhiteSpace(tracesEndpoint))
        {
            openTelemetry.WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource(TraceSources)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(tracesEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));
        }

        if (!string.IsNullOrWhiteSpace(metricsEndpoint))
        {
            openTelemetry.WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Meters)
                .AddOtlpExporter((exporter, reader) =>
                {
                    exporter.Endpoint = new Uri(metricsEndpoint);
                    exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                        configuration.GetValue("OpenTelemetry:MetricsExportIntervalMilliseconds", 15_000);
                }));
        }

        return services;
    }
}
