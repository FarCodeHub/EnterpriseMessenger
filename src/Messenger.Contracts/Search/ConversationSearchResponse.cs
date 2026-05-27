namespace Messenger.Contracts.Search;

public sealed record ConversationSearchResponse(
    Guid Id,
    string? Title,
    string Type,
    DateTime CreatedAtUtc,
    DateTime? LastMessageAtUtc
);