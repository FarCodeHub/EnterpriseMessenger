using System.Text.Json;
using Messenger.Application.Outbox;
using Messenger.Domain.Outbox;
using Messenger.Infrastructure.Persistence;

namespace Messenger.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly MessengerDbContext _dbContext;

    public OutboxWriter(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var type = typeof(TEvent).AssemblyQualifiedName
            ?? typeof(TEvent).FullName
            ?? typeof(TEvent).Name;

        var payload = JsonSerializer.Serialize(integrationEvent);

        _dbContext.OutboxMessages.Add(
            new OutboxMessage(type, payload));

        return Task.CompletedTask;
    }
}