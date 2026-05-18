using Messenger.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class ConversationCounterConfiguration : IEntityTypeConfiguration<ConversationCounter>
{
    public void Configure(EntityTypeBuilder<ConversationCounter> builder)
    {
        builder.ToTable("ConversationCounters");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.ConversationId)
            .IsUnique();
    }
}