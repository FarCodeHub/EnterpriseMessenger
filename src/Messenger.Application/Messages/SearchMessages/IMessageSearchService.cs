namespace Messenger.Application.Messages.SearchMessages;

public interface IMessageSearchService
{
    Task<SearchMessagesResult> SearchAsync(
        SearchMessagesQuery query,
        CancellationToken cancellationToken);
}