namespace Messenger.Application.Realtime;

public interface IReadReceiptService
{
    Task MarkAsSeenAsync(
        Guid conversationId,
        Guid userId,
        long sequenceNumber,
        CancellationToken cancellationToken);
}