using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .IsRequired();

        builder.Property(x => x.OccurredUtc)
            .HasColumnName("occurred_utc")
            .IsRequired();

        builder.Property(x => x.Processed)
            .HasColumnName("processed")
            .IsRequired();

        builder.HasIndex(x => x.Processed);
    }
}
