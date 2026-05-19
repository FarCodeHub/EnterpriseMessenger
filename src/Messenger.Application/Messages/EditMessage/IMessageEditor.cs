namespace Messenger.Application.Messages.EditMessage;

public interface IMessageEditor
{
    Task<EditMessageResult> EditAsync(
        EditMessageCommand command,
        CancellationToken cancellationToken);
}