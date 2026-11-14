namespace Messenger.Contracts.IntegrationEvents;

/// <summary>Written to the outbox when a message is deleted.</summary>
public sealed record MessageDeletedIntegrationEvent(
    Guid MessageId,
    Guid ConversationId,
    long SequenceNumber,
    DateTime DeletedAtUtc);
