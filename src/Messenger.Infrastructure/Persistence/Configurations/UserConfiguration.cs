using Messenger.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.UserName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.UserName)
            .IsUnique();

        builder.Property(x => x.ExternalSubject)
            .HasMaxLength(255);

        builder.HasIndex(x => x.ExternalSubject)
            .IsUnique();
    }
}