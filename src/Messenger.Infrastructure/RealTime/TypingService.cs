using Messenger.Application.Conversations;
using Messenger.Application.Realtime;

namespace Messenger.Infrastructure.Realtime;

public sealed class TypingService : ITypingService
{
    private readonly IConversationPermissionChecker _permissionChecker;

    public TypingService(IConversationPermissionChecker permissionChecker)
    {
        _permissionChecker = permissionChecker;
    }

    public Task StartTypingAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _permissionChecker.EnsureActiveMemberAsync(
            conversationId,
            userId,
            cancellationToken);
    }

    public Task StopTypingAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _permissionChecker.EnsureActiveMemberAsync(
            conversationId,
            userId,
            cancellationToken);
    }
}