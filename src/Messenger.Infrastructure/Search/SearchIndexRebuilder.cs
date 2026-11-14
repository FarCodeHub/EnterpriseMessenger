using Messenger.Application.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messenger.Infrastructure.Search;

/// <summary>
/// Rebuilds the search index outside the HTTP request: indexing hundreds of thousands of messages takes minutes,
/// longer than a proxy waits for a response (nginx cancelled it after 60 s). Stops when the app stops.
/// </summary>
public sealed class SearchIndexRebuilder : ISearchIndexRebuilder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SearchIndexRebuilder> _logger;
    private readonly Lock _lock = new();
    private SearchIndexRebuildStatus _status = SearchIndexRebuildStatus.NeverRun;
    private bool _running;

    public SearchIndexRebuilder(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<SearchIndexRebuilder> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    public SearchIndexRebuildStatus Status
    {
        get
        {
            lock (_lock)
                return _status;
        }
    }

    public SearchIndexRebuildStart Start()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            if (scope.ServiceProvider.GetRequiredService<IMessageSearchIndexer>() is NoOpMessageSearchIndexer)
                return SearchIndexRebuildStart.SearchNotConfigured;
        }

        lock (_lock)
        {
            if (_running)
                return SearchIndexRebuildStart.AlreadyRunning;

            _running = true;
            _status = new SearchIndexRebuildStatus("running", 0, DateTime.UtcNow, null, null);
        }

        _ = Task.Run(RunAsync);
        return SearchIndexRebuildStart.Started;
    }

    private async Task RunAsync()
    {
        var startedAt = Status.StartedAtUtc;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var indexer = scope.ServiceProvider.GetRequiredService<IMessageSearchIndexer>();

            _logger.LogInformation("Search index rebuild started.");

            var indexed = await indexer.RebuildAsync(
                count => Update(new SearchIndexRebuildStatus("running", count, startedAt, null, null)),
                _lifetime.ApplicationStopping);

            Update(new SearchIndexRebuildStatus("completed", indexed, startedAt, DateTime.UtcNow, null));
            _logger.LogInformation("Search index rebuild indexed {Count} messages.", indexed);
        }
        catch (Exception exception)
        {
            Update(Status with { State = "failed", FinishedAtUtc = DateTime.UtcNow, Error = exception.Message });
            _logger.LogError(exception, "Search index rebuild failed.");
        }
        finally
        {
            lock (_lock)
                _running = false;
        }
    }

    private void Update(SearchIndexRebuildStatus status)
    {
        lock (_lock)
            _status = status;
    }
}
