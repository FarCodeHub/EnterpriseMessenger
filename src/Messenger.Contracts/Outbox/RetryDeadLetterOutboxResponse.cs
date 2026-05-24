namespace Messenger.Contracts.Outbox;

public sealed record RetryDeadLetterOutboxResponse(
    Guid DeadLetterId,
    Guid NewOutboxMessageId,
    DateTime RetriedAtUtc
);