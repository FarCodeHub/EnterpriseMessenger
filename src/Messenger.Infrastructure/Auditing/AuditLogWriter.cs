using Messenger.Application.Auditing;
using Messenger.Domain.Auditing;
using Messenger.Infrastructure.Persistence;
using System.Text.Json;

namespace Messenger.Infrastructure.Auditing;

public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly MessengerDbContext _dbContext;

    public AuditLogWriter(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task WriteAsync(
        string eventType,
        Guid? userId,
        string entityType,
        string entityId,
        object data,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data);

        _dbContext.AuditLogs.Add(
            new AuditLog(
                eventType,
                userId,
                entityType,
                entityId,
                json));

        return Task.CompletedTask;
    }
}