namespace Messenger.Application.Messages.EditMessage;

public sealed record EditMessageCommand(
    Guid MessageId,
    Guid UserId,
    string Body
);