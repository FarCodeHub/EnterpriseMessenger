using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Messenger.Infrastructure.Observability;

/// <summary>
/// The messenger's own metrics. They cost nothing when nobody listens; the API exports them through
/// OpenTelemetry when it is configured (see <c>ObservabilitySetup</c> in Messenger.Api).
/// </summary>
public static class MessengerMetrics
{
    public const string MeterName = "Messenger";

    private static readonly Meter Meter = new(MeterName);

    // Bucket boundaries in seconds, the same as ASP.NET Core's request duration. The OpenTelemetry default
    // (0, 5, 10, 25, ... meant for milliseconds) would put every sample in the first bucket.
    private static readonly InstrumentAdvice<double> SecondsBuckets = new()
    {
        HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
    };

    // Outbox delays include the publisher's poll interval and retries, so they reach further.
    private static readonly InstrumentAdvice<double> DelayBuckets = new()
    {
        HistogramBucketBoundaries = [0.1, 0.25, 0.5, 1, 2, 5, 10, 30, 60, 120, 300, 600],
    };

    /// <summary>Messages stored, by <c>kind</c> (send, forward).</summary>
    public static readonly Counter<long> MessagesCreated = Meter.CreateCounter<long>(
        "messenger.messages.created",
        unit: "{message}",
        description: "Messages stored.");

    /// <summary>
    /// Time to store a sent message, including waiting for the conversation's counter lock; by <c>result</c>
    /// (created, duplicate).
    /// </summary>
    public static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>(
        "messenger.message.send.duration",
        unit: "s",
        description: "Time to store a sent message, including lock waits.",
        tags: null,
        advice: SecondsBuckets);

    /// <summary>Time to push a real-time event to a conversation's members, by <c>target</c> (user_groups, conversation_group).</summary>
    public static readonly Histogram<double> RealtimePushDuration = Meter.CreateHistogram<double>(
        "messenger.realtime.push.duration",
        unit: "s",
        description: "Time to push a real-time event to a conversation's members.",
        tags: null,
        advice: SecondsBuckets);

    /// <summary>Real-time pushes that failed; clients catch up through sync.</summary>
    public static readonly Counter<long> RealtimePushFailures = Meter.CreateCounter<long>(
        "messenger.realtime.push.failures",
        unit: "{push}",
        description: "Real-time pushes that failed.");

    /// <summary>Time from writing an outbox event to publishing it, by event <c>type</c>.</summary>
    public static readonly Histogram<double> OutboxPublishDelay = Meter.CreateHistogram<double>(
        "messenger.outbox.publish.delay",
        unit: "s",
        description: "Time from writing an outbox event to publishing it.",
        tags: null,
        advice: DelayBuckets);

    /// <summary>Outbox events that failed to publish, by event <c>type</c>.</summary>
    public static readonly Counter<long> OutboxPublishFailures = Meter.CreateCounter<long>(
        "messenger.outbox.publish.failures",
        unit: "{event}",
        description: "Outbox events that failed to publish.");

    public static double ElapsedSeconds(long startTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
}
