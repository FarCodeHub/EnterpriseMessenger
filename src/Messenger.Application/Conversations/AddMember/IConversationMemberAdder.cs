namespace Messenger.Application.Conversations.AddMember;

public interface IConversationMemberAdder
{
    Task<AddConversationMemberResult> AddAsync(
        AddConversationMemberCommand command,
        CancellationToken cancellationToken);
}