using Messenger.Application.Conversations.CreatePrivateConversation;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class PrivateConversationCreator : IPrivateConversationCreator
{
    private readonly MessengerDbContext _dbContext;

    public PrivateConversationCreator(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreatePrivateConversationResult> CreateAsync(
        CreatePrivateConversationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FirstUserId == command.SecondUserId)
            throw new InvalidOperationException("Private conversation requires two different users.");

        var userIds = new[] { command.FirstUserId, command.SecondUserId };

        var existingUsersCount = await _dbContext.Users
            .CountAsync(x => userIds.Contains(x.Id), cancellationToken);

        if (existingUsersCount != 2)
            throw new InvalidOperationException("One or both users do not exist.");
            



            var existingConversationId = await _dbContext.Conversations
    .Where(conversation => conversation.Type == ConversationType.Private)
    .Where(conversation =>
        _dbContext.ConversationMembers.Any(member =>
            member.ConversationId == conversation.Id &&
            member.UserId == command.FirstUserId &&
            member.LeftAtUtc == null) &&
        _dbContext.ConversationMembers.Any(member =>
            member.ConversationId == conversation.Id &&
            member.UserId == command.SecondUserId &&
            member.LeftAtUtc == null))
    .Select(conversation => (Guid?)conversation.Id)
    .FirstOrDefaultAsync(cancellationToken);

if (existingConversationId.HasValue)
{
    return new CreatePrivateConversationResult(
        existingConversationId.Value,
        (int)ConversationType.Private,
        userIds);
}

        var conversation = new Conversation(
            ConversationType.Private,
            command.FirstUserId);

        _dbContext.Conversations.Add(conversation);

        _dbContext.ConversationCounters.Add(
            new ConversationCounter(conversation.Id));

        _dbContext.ConversationMembers.AddRange(
           new ConversationMember(conversation.Id, command.FirstUserId, ConversationMemberRole.Owner),

           new ConversationMember(conversation.Id, command.SecondUserId, ConversationMemberRole.Member));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatePrivateConversationResult(
            conversation.Id,
            (int)conversation.Type,
            userIds);
    }
}