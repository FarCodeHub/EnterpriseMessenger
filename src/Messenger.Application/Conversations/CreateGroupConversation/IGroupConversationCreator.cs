namespace Messenger.Application.Conversations.CreateGroupConversation;

public interface IGroupConversationCreator
{
    Task<CreateGroupConversationResult> CreateAsync(
        CreateGroupConversationCommand command,
        CancellationToken cancellationToken);
}