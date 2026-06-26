namespace Messenger.Contracts.Messages;

public sealed record ForwardMessageRequest(
    Guid SourceMessageId,
    IReadOnlyCollection<Guid> TargetConversationIds,
    string? Comment);
public sealed record ForwardMessagesRequest(
    IReadOnlyCollection<Guid> SourceMessageIds,
    IReadOnlyCollection<Guid> TargetConversationIds,
    string? Comment);