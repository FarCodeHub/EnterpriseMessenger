using Messenger.Domain.Common;

namespace Messenger.Domain.Auditing;

public class AuditLog : BaseEntity
{
    public string EventType { get; private set; } = null!;

    public Guid? UserId { get; private set; }

    public string EntityType { get; private set; } = null!;

    public string EntityId { get; private set; } = null!;

    public string Data { get; private set; } = null!;

   

    private AuditLog()
    {
    }

    public AuditLog(
        string eventType,
        Guid? userId,
        string entityType,
        string entityId,
        string data)
    {
        EventType = eventType;
        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
        Data = data;
        CreatedAtUtc = DateTime.UtcNow;
    }
}