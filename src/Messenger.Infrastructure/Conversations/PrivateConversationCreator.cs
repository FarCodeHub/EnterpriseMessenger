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

        var conversation = new Conversation(
            ConversationType.Private,
            command.FirstUserId);

        _dbContext.Conversations.Add(conversation);

        _dbContext.ConversationCounters.Add(
            new ConversationCounter(conversation.Id));

        _dbContext.ConversationMembers.AddRange(
            new ConversationMember(conversation.Id, command.FirstUserId),
            new ConversationMember(conversation.Id, command.SecondUserId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatePrivateConversationResult(
            conversation.Id,
            (int)conversation.Type,
            userIds);
    }
}