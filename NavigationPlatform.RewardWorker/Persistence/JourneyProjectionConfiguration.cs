using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class JourneyProjectionConfiguration
    : IEntityTypeConfiguration<JourneyProjection>
{
    public void Configure(EntityTypeBuilder<JourneyProjection> builder)
    {
        builder.ToTable("journey_projection");
        builder.HasKey(x => x.JourneyId);

        builder.Property(x => x.JourneyId).HasColumnName("journey_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Date).HasColumnName("date");
        builder.Property(x => x.DistanceKm)
            .HasColumnName("distance_km")
            .HasPrecision(5, 2);

        builder.HasIndex(x => new { x.UserId, x.Date });
    }
}

