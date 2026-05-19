namespace Messenger.Application.Messages.DeleteMessage;

public sealed record DeleteMessageResult(
    Guid Id,
    Guid ConversationId,
    long SequenceNumber,
    DateTime? DeletedAtUtc
);