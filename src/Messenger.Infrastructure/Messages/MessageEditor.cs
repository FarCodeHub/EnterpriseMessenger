using Messenger.Application.Messages.EditMessage;
using Messenger.Application.Sync;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageEditor : IMessageEditor
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;

    public MessageEditor(MessengerDbContext dbContext, IUserSyncEventWriter syncEventWriter)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
    }

    public async Task<EditMessageResult> EditAsync(
        EditMessageCommand command,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == command.MessageId, cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.SenderUserId != command.UserId)
            throw new UnauthorizedAccessException("You can only edit your own messages.");

        message.Edit(command.Body);

   await _syncEventWriter.AddForConversationMembersAsync(
    message.ConversationId,
    message.Id,
    "MessageEdited",
    new
    {
        MessageId = message.Id,
        message.ConversationId,
        message.SequenceNumber,
        message.Body,
        message.EditedAtUtc
    },
    cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EditMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.SenderUserId,
            message.Body,
            message.EditedAtUtc);
    }
 
}