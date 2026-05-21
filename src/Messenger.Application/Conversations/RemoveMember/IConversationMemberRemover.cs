namespace Messenger.Application.Conversations.RemoveMember;

public interface IConversationMemberRemover
{
    Task<RemoveConversationMemberResult> RemoveAsync(
        RemoveConversationMemberCommand command,
        CancellationToken cancellationToken);
}