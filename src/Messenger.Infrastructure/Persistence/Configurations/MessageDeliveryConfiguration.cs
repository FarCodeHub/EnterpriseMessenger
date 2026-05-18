using Messenger.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Infrastructure.Persistence.Configurations;

public sealed class MessageDeliveryConfiguration : IEntityTypeConfiguration<MessageDelivery>
{
    public void Configure(EntityTypeBuilder<MessageDelivery> builder)
    {
        builder.ToTable("MessageDeliveries");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.MessageId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.DeliveredAtUtc });

        builder.HasIndex(x => new { x.UserId, x.SeenAtUtc });
    }
}