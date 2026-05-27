namespace Messenger.Application.Realtime;

public interface IDeliveryReceiptService
{
    Task MarkAsDeliveredAsync(
        Guid conversationId,
        Guid userId,
        long sequenceNumber,
        CancellationToken cancellationToken);
}