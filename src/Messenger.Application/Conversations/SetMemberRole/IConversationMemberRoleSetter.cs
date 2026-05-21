namespace Messenger.Application.Conversations.SetMemberRole;

public interface IConversationMemberRoleSetter
{
    Task<SetConversationMemberRoleResult> SetAsync(
        SetConversationMemberRoleCommand command,
        CancellationToken cancellationToken);
}