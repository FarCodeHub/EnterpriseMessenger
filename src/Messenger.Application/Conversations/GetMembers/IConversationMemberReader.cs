namespace Messenger.Application.Conversations.GetMembers;

public interface IConversationMemberReader
{
    Task<IReadOnlyCollection<ConversationMemberResult>> GetAsync(
        GetConversationMembersQuery query,
        CancellationToken cancellationToken);
}