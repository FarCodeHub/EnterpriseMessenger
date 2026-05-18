using Messenger.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class ConversationMemberConfiguration : IEntityTypeConfiguration<ConversationMember>
{
    public void Configure(EntityTypeBuilder<ConversationMember> builder)
    {
        builder.ToTable("ConversationMembers");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.ConversationId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ConversationId);
    }
}