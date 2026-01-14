using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class JourneyShareConfiguration : IEntityTypeConfiguration<JourneyShare>
{
    public void Configure(EntityTypeBuilder<JourneyShare> builder)
    {
        builder.ToTable("journey_shares");

        builder.HasKey(x => new { x.JourneyId, x.SharedWithUserId });

        builder.Property(x => x.JourneyId)
            .HasColumnName("journey_id")
            .IsRequired();

        builder.Property(x => x.SharedWithUserId)
            .HasColumnName("shared_with_user_id")
            .IsRequired();

        builder.Property(x => x.SharedAtUtc)
            .HasColumnName("shared_at_utc")
            .IsRequired();
    }
}
