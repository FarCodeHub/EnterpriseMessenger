namespace Messenger.Application.Conversations.GetUserConversations;

public interface IUserConversationReader
{
    Task<IReadOnlyCollection<GetUserConversationResult>> GetAsync(
        GetUserConversationsQuery query,
        CancellationToken cancellationToken);


        Task<IReadOnlyCollection<GetUserConversationResult>> GetArchivedAsync(
    GetUserConversationsQuery query,
    CancellationToken cancellationToken);
}