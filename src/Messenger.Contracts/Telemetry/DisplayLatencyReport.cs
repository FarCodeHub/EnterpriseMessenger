namespace Messenger.Contracts.Telemetry;

/// <summary>
/// Send-to-display samples a client collected since its last report, in milliseconds: how long after the server
/// stored each message (its <c>createdAtUtc</c>) the client drew it on screen, after correcting its clock offset.
/// </summary>
public sealed record DisplayLatencyReport(IReadOnlyList<double> SamplesMs);
