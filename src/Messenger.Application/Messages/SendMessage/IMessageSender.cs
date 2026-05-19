namespace Messenger.Application.Messages.SendMessage;

public interface IMessageSender
{
    Task<SendMessageResult> SendAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken);
}