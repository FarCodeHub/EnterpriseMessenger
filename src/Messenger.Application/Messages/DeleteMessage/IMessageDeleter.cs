namespace Messenger.Application.Messages.DeleteMessage;

public interface IMessageDeleter
{
    Task<DeleteMessageResult> DeleteAsync(
        DeleteMessageCommand command,
        CancellationToken cancellationToken);
}