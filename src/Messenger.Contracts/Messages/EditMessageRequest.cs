namespace Messenger.Contracts.Messages;

public sealed record EditMessageRequest(
    Guid UserId,
    string Body
);