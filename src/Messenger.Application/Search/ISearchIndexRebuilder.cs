namespace Messenger.Application.Search;

/// <summary>Runs <see cref="IMessageSearchIndexer.RebuildAsync"/> in the background, one rebuild at a time.</summary>
public interface ISearchIndexRebuilder
{
    SearchIndexRebuildStart Start();

    SearchIndexRebuildStatus Status { get; }
}

public enum SearchIndexRebuildStart
{
    Started,
    AlreadyRunning,
    SearchNotConfigured,
}

public sealed record SearchIndexRebuildStatus(
    string State,
    long Indexed,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc,
    string? Error)
{
    public static readonly SearchIndexRebuildStatus NeverRun = new("idle", 0, null, null, null);
}
