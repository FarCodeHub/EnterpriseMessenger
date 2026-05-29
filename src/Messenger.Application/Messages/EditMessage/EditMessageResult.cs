namespace Messenger.Application.Messages.EditMessage;

public sealed record EditMessageResult(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    Guid SenderUserId,
    string? SenderDisplayName,
    string Body,
    DateTime? EditedAtUtc
);