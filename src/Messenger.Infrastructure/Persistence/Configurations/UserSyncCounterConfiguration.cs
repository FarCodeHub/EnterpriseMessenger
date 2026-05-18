using Messenger.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class UserSyncCounterConfiguration : IEntityTypeConfiguration<UserSyncCounter>
{
    public void Configure(EntityTypeBuilder<UserSyncCounter> builder)
    {
        builder.ToTable("UserSyncCounters");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
            .IsUnique();
    }
}