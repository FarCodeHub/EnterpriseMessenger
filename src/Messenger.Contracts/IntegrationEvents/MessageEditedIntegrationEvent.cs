namespace Messenger.Contracts.IntegrationEvents;

/// <summary>Written to the outbox when a message's text changes.</summary>
public sealed record MessageEditedIntegrationEvent(
    Guid MessageId,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    string Body,
    DateTime EditedAtUtc);
