using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Messenger.Api.Observability;
using Messenger.Contracts.Conversations;
using Messenger.Contracts.Messages;
using Messenger.Contracts.Users;
using Messenger.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Messenger.IntegrationTests;

/// <summary>Runs alone: the messenger meter is process-wide, so parallel tests would add to its counts.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MetricsCollection
{
    public const string Name = "metrics";
}

[Collection(MetricsCollection.Name)]
public sealed class MessengerMetricsTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<(string Name, double Value, string Tags)> _measurements = new();

    public MessengerMetricsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MessengerMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Enqueue((instrument.Name, value, Describe(tags))));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _measurements.Enqueue((instrument.Name, value, Describe(tags))));
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task Send_Duplicate_And_Forward_Record_Metrics()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var withBob = await CreatePrivateConversationAsync(alice, bob);
        var withCarol = await CreatePrivateConversationAsync(alice, carol);
        _measurements.Clear();

        var clientMessageId = Guid.NewGuid();
        var sent = await SendAsync(new SendMessageRequest(withBob, clientMessageId, "Hello"));
        await SendAsync(new SendMessageRequest(withBob, clientMessageId, "Hello")); // retry of the same message

        var forward = await _client.PostAsJsonAsync(
            "/api/v1/messages/forward", new ForwardMessageRequest(sent, [withBob, withCarol], null));
        forward.StatusCode.Should().Be(HttpStatusCode.OK);

        Values("messenger.messages.created", "kind=send").Should().Equal(1);
        Values("messenger.messages.created", "kind=forward").Should().Equal(2);
        Values("messenger.message.send.duration", "result=created").Should().ContainSingle().Which.Should().BePositive();
        Values("messenger.message.send.duration", "result=duplicate").Should().ContainSingle();

        // One push per message.created / conversation.updated event: 2 for the send, 2 per forwarded message.
        Values("messenger.realtime.push.duration", "target=user_groups").Should().HaveCountGreaterThanOrEqualTo(6);
        Values("messenger.realtime.push.failures", "").Should().BeEmpty();
    }

    private List<double> Values(string name, string tags) =>
        _measurements.Where(x => x.Name == name && x.Tags == tags).Select(x => x.Value).ToList();

    private static string Describe(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var parts = new List<string>();

        foreach (var tag in tags)
            parts.Add($"{tag.Key}={tag.Value}");

        return string.Join(",", parts);
    }

    private async Task<Guid> SendAsync(SendMessageRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/messages", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreateUserAsync(string displayName)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/users", new CreateUserRequest(displayName, $"user-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreatePrivateConversationAsync(Guid first, Guid second)
    {
        SetCurrentUser(first);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/conversations/private", new CreatePrivateConversationRequest(first, second));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private void SetCurrentUser(Guid userId)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-UserId");
        _client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }

    private sealed record IdResponse(Guid Id);
}

public sealed class ObservabilitySetupTests
{
    [Fact]
    public void Without_Endpoints_Nothing_Is_Registered()
    {
        using var provider = Build(new Dictionary<string, string?>());

        provider.GetService<MeterProvider>().Should().BeNull();
        provider.GetService<TracerProvider>().Should().BeNull();
    }

    [Fact]
    public void Endpoints_Turn_On_Metrics_And_Traces_Independently()
    {
        using (var metricsOnly = Build(new() { ["OpenTelemetry:MetricsEndpoint"] = "http://localhost:9090/api/v1/otlp/v1/metrics" }))
        {
            metricsOnly.GetService<MeterProvider>().Should().NotBeNull();
            metricsOnly.GetService<TracerProvider>().Should().BeNull();
        }

        using var tracesOnly = Build(new() { ["OpenTelemetry:TracesEndpoint"] = "http://localhost:4318/v1/traces" });
        tracesOnly.GetService<TracerProvider>().Should().NotBeNull();
        tracesOnly.GetService<MeterProvider>().Should().BeNull();
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddLogging()
            .AddMessengerObservability(configuration)
            .BuildServiceProvider();
    }
}
