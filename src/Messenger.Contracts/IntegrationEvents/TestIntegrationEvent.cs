namespace Messenger.Contracts.IntegrationEvents;

public sealed record TestIntegrationEvent(
    Guid Id,
    string Message,
    DateTime CreatedAtUtc
);