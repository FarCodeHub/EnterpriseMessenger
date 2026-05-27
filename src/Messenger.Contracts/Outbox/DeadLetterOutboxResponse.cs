namespace Messenger.Contracts.Outbox;

public sealed record DeadLetterOutboxResponse(
    Guid Id,
    Guid OriginalOutboxMessageId,
    string Type,
    string Error,
    int RetryCount,
    DateTime MovedAtUtc
);