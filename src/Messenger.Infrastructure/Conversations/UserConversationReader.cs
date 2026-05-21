using Messenger.Application.Conversations.GetUserConversations;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class UserConversationReader : IUserConversationReader
{
    private readonly MessengerDbContext _dbContext;

    public UserConversationReader(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IReadOnlyCollection<GetUserConversationResult>> GetAsync(
        GetUserConversationsQuery query,
        CancellationToken cancellationToken)
    {
        return GetConversationsAsync(
            query.UserId,
            isArchived: false,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<GetUserConversationResult>> GetArchivedAsync(
        GetUserConversationsQuery query,
        CancellationToken cancellationToken)
    {
        return GetConversationsAsync(
            query.UserId,
            isArchived: true,
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<GetUserConversationResult>> GetConversationsAsync(
        Guid userId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        var conversations = await _dbContext.ConversationMembers
            .Where(member =>
                member.UserId == userId &&
                member.IsArchived == isArchived)
            .Join(
                _dbContext.Conversations,
                member => member.ConversationId,
                conversation => conversation.Id,
                (member, conversation) => new
                {
                    conversation.Id,
                    Type = (int)conversation.Type,
                    conversation.Title,
                    conversation.CreatedAtUtc,
                    conversation.LastMessageId,
                    conversation.LastMessageAtUtc,
                    member.LastReadSequenceNumber,
                    member.IsPinned
                })
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var conversationIds = conversations
            .Select(x => x.Id)
            .ToList();

        var unreadCounts = await _dbContext.Messages
            .Where(message => conversationIds.Contains(message.ConversationId))
            .GroupJoin(
                _dbContext.ConversationMembers.Where(member => member.UserId == userId),
                message => message.ConversationId,
                member => member.ConversationId,
                (message, members) => new
                {
                    Message = message,
                    Member = members.FirstOrDefault()
                })
            .Where(x =>
                x.Member != null &&
                x.Message.SequenceNumber > x.Member.LastReadSequenceNumber &&
                x.Message.SenderUserId != userId)
            .GroupBy(x => x.Message.ConversationId)
            .Select(x => new
            {
                ConversationId = x.Key,
                Count = x.Count()
            })
            .ToDictionaryAsync(
                x => x.ConversationId,
                x => x.Count,
                cancellationToken);

        return conversations
            .Select(x => new GetUserConversationResult(
                x.Id,
                x.Type,
                x.Title,
                x.CreatedAtUtc,
                x.LastMessageId,
                x.LastMessageAtUtc,
                x.LastReadSequenceNumber,
                unreadCounts.TryGetValue(x.Id, out var count) ? count : 0,
                x.IsPinned))
            .ToList();
    }
}