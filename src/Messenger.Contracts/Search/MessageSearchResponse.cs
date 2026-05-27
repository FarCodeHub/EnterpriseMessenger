namespace Messenger.Contracts.Search;

public sealed record MessageSearchResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string Body,
    string? Highlight,
    long SequenceNumber,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc
);