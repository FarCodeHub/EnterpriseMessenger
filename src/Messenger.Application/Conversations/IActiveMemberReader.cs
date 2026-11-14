namespace Messenger.Application.Conversations;

public interface IActiveMemberReader
{
    /// <summary>
    /// User ids of the conversation's members who have not left it, at most <paramref name="take"/> of them.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(
        Guid conversationId,
        int take,
        CancellationToken cancellationToken);
}
