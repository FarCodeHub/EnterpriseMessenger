using Messenger.Application.Conversations.GetUserConversations;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
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
                member.LeftAtUtc == null &&
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
                    member.IsPinned,
                    member.IsMuted,
                    member.IsArchived
                })
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var conversationIds = conversations
            .Select(x => x.Id)
            .ToList();

        var unreadCounts = await _dbContext.Messages
            .Where(message =>
                conversationIds.Contains(message.ConversationId) &&
                message.DeletedAtUtc == null)
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

        var lastMessages = await _dbContext.Messages
            .Where(message =>
                conversationIds.Contains(message.ConversationId) &&
                message.DeletedAtUtc == null)
            .GroupBy(message => message.ConversationId)
            .Select(group => group
                .OrderByDescending(message => message.SequenceNumber)
                .Select(message => new
                {
                    message.Id,
                    message.ConversationId,
                    message.Body,
                    message.SenderUserId,
                    message.CreatedAtUtc,
                    message.SequenceNumber
                })
                .First())
            .ToDictionaryAsync(
                x => x.ConversationId,
                x => x,
                cancellationToken);

        var senderUserIds = lastMessages.Values
            .Select(x => x.SenderUserId)
            .Distinct()
            .ToList();

        var senders = await _dbContext.Users
            .Where(user => senderUserIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.UserName
            })
            .ToDictionaryAsync(
                x => x.Id,
                x => x,
                cancellationToken);

        var otherUsers = await _dbContext.ConversationMembers
            .Where(member =>
                conversationIds.Contains(member.ConversationId) &&
                member.UserId != userId &&
                member.LeftAtUtc == null)
            .Join(
                _dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new
                {
                    member.ConversationId,
                    user.Id,
                    user.DisplayName,
                    user.UserName
                })
            .GroupBy(x => x.ConversationId)
            .Select(group => group.First())
            .ToDictionaryAsync(
                x => x.ConversationId,
                x => x,
                cancellationToken);

        return conversations
            .Select(conversation =>
            {
                lastMessages.TryGetValue(conversation.Id, out var lastMessage);

                var lastMessagePreview = string.IsNullOrWhiteSpace(lastMessage?.Body)
                    ? null
                    : lastMessage.Body;

                string? lastMessageSenderDisplayName = null;

                if (lastMessage is not null &&
                    senders.TryGetValue(lastMessage.SenderUserId, out var sender))
                {
                    lastMessageSenderDisplayName =
                        sender.DisplayName ?? sender.UserName;
                }

                var displayTitle = conversation.Title;

                if (string.IsNullOrWhiteSpace(displayTitle) &&
                    otherUsers.TryGetValue(conversation.Id, out var otherUser))
                {
                    displayTitle = otherUser.DisplayName ?? otherUser.UserName;
                }

                if (string.IsNullOrWhiteSpace(displayTitle))
                {
                    displayTitle = conversation.Type == (int)ConversationType.Private
                        ? "Unknown User"
                        : "Untitled Group";
                }

                return new GetUserConversationResult(
                    conversation.Id,
                    conversation.Type,
                    conversation.Title,
                    displayTitle,
                    conversation.CreatedAtUtc,
                    conversation.LastMessageId,
                    conversation.LastMessageAtUtc,
                    lastMessagePreview,
                    lastMessage?.SenderUserId,
                    lastMessageSenderDisplayName,
                    conversation.LastReadSequenceNumber,
                    unreadCounts.TryGetValue(conversation.Id, out var count) ? count : 0,
                    conversation.IsPinned,
                    conversation.IsMuted,
                    conversation.IsArchived);
            })
            .ToList();
    }
}