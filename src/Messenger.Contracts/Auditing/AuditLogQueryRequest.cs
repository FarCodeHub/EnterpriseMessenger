namespace Messenger.Contracts.Auditing;

public sealed record AuditLogQueryRequest(
    string? EventType,
    Guid? UserId,
    string? EntityType,
    string? EntityId,
    int Page = 1,
    int PageSize = 50
);