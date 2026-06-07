namespace Messenger.Application.Messages.ForwardMessage;

public sealed record ForwardMessageCommand(
    Guid SourceMessageId,
    Guid CurrentUserId,
    IReadOnlyCollection<Guid> TargetConversationIds,
    string? Comment);

    public sealed record ForwardMessagesCommand(
    IReadOnlyCollection<Guid> SourceMessageIds,
    Guid CurrentUserId,
    IReadOnlyCollection<Guid> TargetConversationIds,
    string? Comment);