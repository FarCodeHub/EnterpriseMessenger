namespace Messenger.Application.Realtime;

public interface ITypingService
{
    Task StartTypingAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task StopTypingAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken);
}