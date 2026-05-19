namespace Messenger.Application.Conversations.GetUserConversations;

public interface IUserConversationReader
{
    Task<IReadOnlyCollection<GetUserConversationResult>> GetAsync(
        GetUserConversationsQuery query,
        CancellationToken cancellationToken);
}