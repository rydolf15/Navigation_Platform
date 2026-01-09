using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NavigationPlatform.RewardWorker.Persistence;

internal sealed class DailyDistanceProjectionConfiguration
    : IEntityTypeConfiguration<DailyDistanceProjection>
{
    public void Configure(EntityTypeBuilder<DailyDistanceProjection> builder)
    {
        builder.ToTable("daily_distance_projection");
        builder.HasKey(x => new { x.UserId, x.Date });

        builder.Property(x => x.TotalDistanceKm)
            .HasPrecision(5, 2);
    }
}