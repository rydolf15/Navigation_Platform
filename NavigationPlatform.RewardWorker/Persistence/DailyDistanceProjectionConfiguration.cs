using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class DailyDistanceProjectionConfiguration
    : IEntityTypeConfiguration<DailyDistanceProjection>
{
    public void Configure(EntityTypeBuilder<DailyDistanceProjection> builder)
    {
        builder.ToTable("daily_distance_projection");
        builder.HasKey(x => new { x.UserId, x.Date });

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Date).HasColumnName("date");

        builder.Property(x => x.TotalDistanceKm)
            .HasColumnName("total_distance_km")
            .HasPrecision(5, 2);

        builder.Property(x => x.RewardGranted)
            .HasColumnName("reward_granted")
            .IsRequired();

        builder.Property(x => x.GrantedByJourneyId)
            .HasColumnName("granted_by_journey_id");
    }
}