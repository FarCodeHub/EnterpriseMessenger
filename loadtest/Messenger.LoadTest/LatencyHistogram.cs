namespace Messenger.LoadTest;

/// <summary>Thread-safe latency histogram with 1 ms buckets up to 2 minutes; exact enough for percentiles.</summary>
public sealed class LatencyHistogram
{
    private const int MaxMs = 120_000;
    private readonly long[] _buckets = new long[MaxMs + 2];
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public void Record(double milliseconds)
    {
        var bucket = milliseconds < 0 ? 0 : milliseconds > MaxMs ? MaxMs + 1 : (int)Math.Round(milliseconds);
        Interlocked.Increment(ref _buckets[bucket]);
        Interlocked.Increment(ref _count);
    }

    /// <summary>Latency in ms at the given quantile (0..1); null without samples. Above 120 s reports 120001.</summary>
    public double? Quantile(double quantile)
    {
        var total = Count;

        if (total == 0)
            return null;

        var rank = (long)Math.Ceiling(quantile * total);
        long seen = 0;

        for (var ms = 0; ms < _buckets.Length; ms++)
        {
            seen += Interlocked.Read(ref _buckets[ms]);

            if (seen >= rank)
                return ms;
        }

        return MaxMs + 1;
    }

    public object Summary() => new
    {
        Count,
        P50 = Quantile(0.50),
        P95 = Quantile(0.95),
        P99 = Quantile(0.99),
        Max = Quantile(1.0),
    };
}
