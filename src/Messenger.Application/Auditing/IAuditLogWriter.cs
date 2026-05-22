namespace Messenger.Application.Auditing;

public interface IAuditLogWriter
{
    Task WriteAsync(
        string eventType,
        Guid? userId,
        string entityType,
        string entityId,
        object data,
        CancellationToken cancellationToken);
}