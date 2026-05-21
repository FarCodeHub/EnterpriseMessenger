namespace Messenger.Contracts.IntegrationEvents;

public sealed record MessageCreatedIntegrationEvent(
    Guid MessageId,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    string Body,
    DateTime CreatedAtUtc
);