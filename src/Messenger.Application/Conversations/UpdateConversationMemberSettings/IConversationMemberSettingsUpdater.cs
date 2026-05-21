namespace Messenger.Application.Conversations.UpdateConversationMemberSettings;

public interface IConversationMemberSettingsUpdater
{
    Task<UpdateConversationMemberSettingsResult> UpdateAsync(
        UpdateConversationMemberSettingsCommand command,
        CancellationToken cancellationToken);
}