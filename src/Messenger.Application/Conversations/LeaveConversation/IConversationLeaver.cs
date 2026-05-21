namespace Messenger.Application.Conversations.LeaveConversation;

public interface IConversationLeaver
{
    Task<LeaveConversationResult> LeaveAsync(
        LeaveConversationCommand command,
        CancellationToken cancellationToken);
}