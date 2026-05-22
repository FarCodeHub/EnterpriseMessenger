using Messenger.Domain.Enums;

namespace Messenger.Application.Conversations;

public interface IConversationPermissionChecker
{
    Task EnsureActiveMemberAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task EnsureOwnerOrAdminAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task EnsureOwnerAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken);


        Task<ConversationMemberRole> GetActiveMemberRoleAsync(
    Guid conversationId,
    Guid userId,
    CancellationToken cancellationToken);
}