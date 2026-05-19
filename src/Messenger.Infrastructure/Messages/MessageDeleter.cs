using Messenger.Application.Messages.DeleteMessage;
using Messenger.Application.Sync;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Infrastructure.Messages;

public sealed class MessageDeleter : IMessageDeleter
{
    private readonly MessengerDbContext _dbContext;
    private readonly IUserSyncEventWriter _syncEventWriter;

    public MessageDeleter(MessengerDbContext dbContext, IUserSyncEventWriter syncEventWriter)
    {
        _dbContext = dbContext;
        _syncEventWriter = syncEventWriter;
    }

    public async Task<DeleteMessageResult> DeleteAsync(
        DeleteMessageCommand command,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(x => x.Id == command.MessageId, cancellationToken);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.SenderUserId != command.UserId)
            throw new UnauthorizedAccessException("You can only delete your own messages.");

        message.Delete();

        await _syncEventWriter.AddForConversationMembersAsync(
          message.ConversationId,
          message.Id,
          "MessageDeleted",
          new
          {
              MessageId = message.Id,
              message.ConversationId,
              message.SequenceNumber,
              message.DeletedAtUtc
          },
          cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteMessageResult(
            message.Id,
            message.ConversationId,
            message.SequenceNumber,
            message.DeletedAtUtc);
    }


}