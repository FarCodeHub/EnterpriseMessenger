using Messenger.Application.Conversations;
using Messenger.Application.Realtime;

namespace Messenger.Infrastructure.Realtime;

public sealed class DeliveryReceiptService : IDeliveryReceiptService
{
    private readonly IConversationPermissionChecker _permissionChecker;

    public DeliveryReceiptService(
        IConversationPermissionChecker permissionChecker)
    {
        _permissionChecker = permissionChecker;
    }

    public Task MarkAsDeliveredAsync(
        Guid conversationId,
        Guid userId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        return _permissionChecker.EnsureActiveMemberAsync(
            conversationId,
            userId,
            cancellationToken);
    }
}