using Messenger.Application.Conversations.UpdateConversationMemberSettings;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Conversations;

public sealed class ConversationMemberSettingsUpdater : IConversationMemberSettingsUpdater
{
    private readonly MessengerDbContext _dbContext;

    public ConversationMemberSettingsUpdater(MessengerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UpdateConversationMemberSettingsResult> UpdateAsync(
        UpdateConversationMemberSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(x =>
                x.ConversationId == command.ConversationId &&
                x.UserId == command.UserId &&
                x.LeftAtUtc == null,
                cancellationToken);

        if (member is null)
            throw new InvalidOperationException("User is not a member of this conversation.");

        if (command.IsMuted is not null)
        {
            if (command.IsMuted.Value)
                member.Mute();
            else
                member.Unmute();
        }

        if (command.IsPinned is not null)
        {
            if (command.IsPinned.Value)
                member.Pin();
            else
                member.Unpin();
        }

        if (command.IsArchived is not null)
        {
            if (command.IsArchived.Value)
                member.Archive();
            else
                member.Unarchive();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateConversationMemberSettingsResult(
            command.ConversationId,
            command.UserId,
            member.IsMuted,
            member.IsPinned,
            member.IsArchived);
    }
}