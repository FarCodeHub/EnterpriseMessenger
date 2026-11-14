using Messenger.Api.Common;
using Messenger.Contracts.Telemetry;
using Messenger.Infrastructure.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Messenger.Api.Controllers;

/// <summary>Measurements that only the clients can make.</summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public sealed class TelemetryController : ControllerBase
{
    public const int MaxSamplesPerReport = 200;

    // Samples outside this range come from a wrong clock offset or a message shown long after it arrived
    // (e.g. read from the offline cache); they would only distort the histogram.
    private const double MaxPlausibleMs = 60_000;
    private const double MinPlausibleMs = -1_000;

    [HttpPost("display-latency")]
    [EnableRateLimiting("messages")]
    public IActionResult ReportDisplayLatency(DisplayLatencyReport report)
    {
        if (report.SamplesMs is null || report.SamplesMs.Count > MaxSamplesPerReport)
        {
            return BadRequest(new ApiErrorResponse(
                "validation_error",
                $"Send between 0 and {MaxSamplesPerReport} samples.",
                StatusCodes.Status400BadRequest,
                DateTime.UtcNow));
        }

        var recorded = 0;

        foreach (var sampleMs in report.SamplesMs)
        {
            if (!double.IsFinite(sampleMs) || sampleMs < MinPlausibleMs || sampleMs > MaxPlausibleMs)
                continue;

            // The clock offset is estimated to within half a round trip, so very fast deliveries can come out
            // slightly negative; they count as instant.
            MessengerMetrics.DisplayLatency.Record(Math.Max(0, sampleMs) / 1000);
            recorded++;
        }

        return Ok(new { Recorded = recorded });
    }
}
