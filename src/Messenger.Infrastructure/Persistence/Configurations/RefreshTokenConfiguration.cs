using Messenger.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.DeviceId)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.UserId, x.DeviceId });
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}