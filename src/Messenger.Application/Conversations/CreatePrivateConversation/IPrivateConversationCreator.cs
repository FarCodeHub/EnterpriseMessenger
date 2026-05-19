namespace Messenger.Application.Conversations.CreatePrivateConversation;

public interface IPrivateConversationCreator
{
    Task<CreatePrivateConversationResult> CreateAsync(
        CreatePrivateConversationCommand command,
        CancellationToken cancellationToken);
}