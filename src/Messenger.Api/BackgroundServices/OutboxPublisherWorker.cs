using Messenger.Application.Outbox;

namespace Messenger.Api.BackgroundServices;

/// <summary>
/// Publishes outbox events to the bus. While there is a backlog it takes batch after batch without waiting;
/// when it has caught up it polls every <c>Outbox:PollIntervalMilliseconds</c>.
/// </summary>
public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _pollInterval;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _batchSize = configuration.GetValue("Outbox:BatchSize", 200);
        _pollInterval = TimeSpan.FromMilliseconds(configuration.GetValue("Outbox:PollIntervalMilliseconds", 1_000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

                published = await publisher.PublishBatchAsync(_batchSize, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Outbox publisher worker failed. It will retry later.");
            }

            // A full batch means more is waiting: take the next one right away.
            if (published < _batchSize)
                await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
