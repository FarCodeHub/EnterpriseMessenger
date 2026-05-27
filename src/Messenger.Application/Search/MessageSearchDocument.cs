namespace Messenger.Application.Search;

public sealed record MessageSearchDocument(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string Body,
    long SequenceNumber,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    bool IsDeleted
);