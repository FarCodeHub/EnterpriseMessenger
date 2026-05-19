namespace Messenger.Application.Messages.EditMessage;

public sealed record EditMessageResult(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    string Body,
    DateTime? EditedAtUtc
);