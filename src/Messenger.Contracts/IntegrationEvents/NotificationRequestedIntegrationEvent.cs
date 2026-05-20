namespace Messenger.Contracts.IntegrationEvents;

public sealed record NotificationRequestedIntegrationEvent(
    Guid NotificationId,
    Guid MessageId,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    DateTime RequestedAtUtc
);