namespace Messenger.Contracts.Auditing;

public sealed record AuditLogResponse(
    Guid Id,
    string EventType,
    Guid? UserId,
    string EntityType,
    string EntityId,
    string Data,
    DateTime CreatedAtUtc
);