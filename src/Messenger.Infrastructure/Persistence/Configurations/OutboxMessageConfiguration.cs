using Messenger.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("jsonb");
            builder.Property(x => x.RetryCount)
    .IsRequired();

builder.HasIndex(x => new { x.ProcessedAtUtc, x.RetryCount, x.OccurredAtUtc });

        builder.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc });
    }
}