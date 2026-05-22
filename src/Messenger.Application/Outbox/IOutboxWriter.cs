namespace Messenger.Application.Outbox;

public interface IOutboxWriter
{
    Task AddAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken);
}