namespace Messenger.Application.Sync.GetSyncEvents;

public sealed record SyncEventResult(
    long EventId,
    string EventType,
    Guid? ConversationId,
    Guid? MessageId,
    string PayloadJson,
    DateTime CreatedAtUtc
);