using Messenger.Application.Messages.SendMessage;

namespace Messenger.Application.Messages.ForwardMessage;

public interface IMessageForwarder
{
    Task<IReadOnlyCollection<SendMessageResult>> ForwardAsync(
        ForwardMessageCommand command,
        CancellationToken cancellationToken);
}