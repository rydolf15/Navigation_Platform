using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class InboxMessageConfiguration
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.OccurredUtc).HasColumnName("occurred_utc").IsRequired();
        builder.Property(x => x.ProcessedUtc).HasColumnName("processed_utc").IsRequired();
    }
}

