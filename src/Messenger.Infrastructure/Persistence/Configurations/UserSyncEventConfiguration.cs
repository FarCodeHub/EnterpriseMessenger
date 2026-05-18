using Messenger.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class UserSyncEventConfiguration : IEntityTypeConfiguration<UserSyncEvent>
{
    public void Configure(EntityTypeBuilder<UserSyncEvent> builder)
    {
        builder.ToTable("UserSyncEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PayloadJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasIndex(x => new { x.UserId, x.EventId })
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}