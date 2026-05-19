namespace Messenger.Application.Conversations.MarkConversationRead;

public interface IConversationReadMarker
{
    Task<MarkConversationReadResult> MarkAsReadAsync(
        MarkConversationReadCommand command,
        CancellationToken cancellationToken);
}