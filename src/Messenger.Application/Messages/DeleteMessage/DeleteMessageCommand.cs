namespace Messenger.Application.Messages.DeleteMessage;

public sealed record DeleteMessageCommand(
    Guid MessageId,
    Guid UserId
);