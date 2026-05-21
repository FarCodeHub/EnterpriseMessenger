using Messenger.Application.Auditing;
using Messenger.Application.Conversations.AddMember;
using Messenger.Domain.Conversations;
using Messenger.Domain.Enums;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberAdder : IConversationMemberAdder
{
    private readonly MessengerDbContext _dbContext;
    private readonly IAuditLogWriter _auditLogWriter;

    public ConversationMemberAdder(MessengerDbContext dbContext, IAuditLogWriter auditLogWriter)
    {
        _dbContext = dbContext;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<AddConversationMemberResult> AddAsync(
        AddConversationMemberCommand command,
        CancellationToken cancellationToken)
    {
        var currentMember = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.CurrentUserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (currentMember is null ||
            (currentMember.Role != ConversationMemberRole.Owner &&
             currentMember.Role != ConversationMemberRole.Admin))
        {
            throw new UnauthorizedAccessException("Only owner or admin can manage members.");
        }

        var userExists = await _dbContext.Users
            .AnyAsync(x => x.Id == command.UserIdToAdd, cancellationToken);

        if (!userExists)
            throw new InvalidOperationException("User not found.");

        var existingMember = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.UserIdToAdd,
                cancellationToken);

        if (existingMember is not null)
        {
            existingMember.Rejoin();

            await _auditLogWriter.WriteAsync(
    "MemberAdded",
    command.CurrentUserId,
    "Conversation",
    command.ConversationId.ToString(),
    new
    {
        command.UserIdToAdd,
        Rejoined = existingMember is not null
    },
    cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AddConversationMemberResult(
                command.ConversationId,
                command.UserIdToAdd,
                Rejoined: true);
        }

        _dbContext.ConversationMembers.Add(
            new ConversationMember(
                command.ConversationId,
                command.UserIdToAdd,
                ConversationMemberRole.Member));


                await _auditLogWriter.WriteAsync(
    "MemberAdded",
    command.CurrentUserId,
    "Conversation",
    command.ConversationId.ToString(),
    new
    {
        command.UserIdToAdd,
        Rejoined = existingMember is not null
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddConversationMemberResult(
            command.ConversationId,
            command.UserIdToAdd,
            Rejoined: false);
    }
}