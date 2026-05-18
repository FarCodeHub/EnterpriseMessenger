using Messenger.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("UserDevices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DeviceName)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(x => new { x.UserId, x.DeviceId })
            .IsUnique();

        builder.HasIndex(x => x.LastSyncedEventId);
    }
}