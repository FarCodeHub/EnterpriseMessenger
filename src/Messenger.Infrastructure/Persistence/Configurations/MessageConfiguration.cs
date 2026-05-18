using Messenger.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Body)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasIndex(x => new { x.ConversationId, x.SequenceNumber })
            .IsUnique();

        builder.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });

        builder.HasIndex(x => new { x.ConversationId, x.ReplyToMessageId });

        builder.HasIndex(x => new { x.SenderUserId, x.CreatedAtUtc });

        builder.HasIndex(x => new { x.ConversationId, x.ClientMessageId })
            .IsUnique();
    }
}