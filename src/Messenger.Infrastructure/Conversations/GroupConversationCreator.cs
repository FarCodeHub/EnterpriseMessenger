using Messenger.Application.Conversations.CreateGroupConversation;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class GroupConversationCreator : IGroupConversationCreator
{
    private readonly MessengerDbContext _dbContext;

    public GroupConversationCreator(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateGroupConversationResult> CreateAsync(
        CreateGroupConversationCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
            throw new InvalidOperationException("Group title is required.");

        var memberUserIds = command.MemberUserIds
            .Append(command.CreatedByUserId)
            .Distinct()
            .ToList();

        if (memberUserIds.Count < 2)
            throw new InvalidOperationException("Group requires at least two members.");

        var existingUsersCount = await _dbContext.Users
            .CountAsync(x => memberUserIds.Contains(x.Id), cancellationToken);

        if (existingUsersCount != memberUserIds.Count)
            throw new InvalidOperationException("Some users do not exist.");

        var conversation = new Conversation(
            ConversationType.Group,
            command.CreatedByUserId,
            command.Title);

        _dbContext.Conversations.Add(conversation);

        _dbContext.ConversationCounters.Add(
            new ConversationCounter(conversation.Id));

        foreach (var userId in memberUserIds)
        {

var role = userId == command.CreatedByUserId
    ? ConversationMemberRole.Owner
    : ConversationMemberRole.Member;

         _dbContext.ConversationMembers.Add(
    new ConversationMember(conversation.Id, userId, role));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateGroupConversationResult(
            conversation.Id,
            (int)conversation.Type,
            conversation.Title,
            memberUserIds);
    }
}