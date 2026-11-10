namespace Messenger.Contracts.Messages;

public sealed record SendMessageRequest(
    Guid ConversationId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId = null,
    List<SendMessageAttachmentRequest>? Attachments = null
);

/// <summary>
/// An attachment for a new message. Only <see cref="FileId"/> (the id returned by the upload endpoint) is used;
/// file name, type, size and storage path are taken from the stored upload record.
/// </summary>
public sealed record SendMessageAttachmentRequest(
    Guid? FileId = null,
    Guid? Id = null,
    string? FileName = null,
    string? ContentType = null,
    long? Size = null,
    string? StoragePath = null
);