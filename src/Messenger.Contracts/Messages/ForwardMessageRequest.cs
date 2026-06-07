namespace Messenger.Contracts.Messages;

public sealed record ForwardMessageRequest(
    Guid SourceMessageId,
    IReadOnlyCollection<Guid> TargetConversationIds,
    string? Comment);