using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NavigationPlatform.Domain.Journeys;

namespace NavigationPlatform.Infrastructure.Persistence.Configurations;

internal sealed class JourneyConfiguration : IEntityTypeConfiguration<Journey>
{
    public void Configure(EntityTypeBuilder<Journey> builder)
    {
        builder.ToTable("journeys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.StartLocation)
            .HasColumnName("start_location")
            .IsRequired();

        builder.Property(x => x.ArrivalLocation)
            .HasColumnName("arrival_location")
            .IsRequired();

        builder.Property(x => x.StartTime)
            .HasColumnName("start_time");

        builder.Property(x => x.ArrivalTime)
            .HasColumnName("arrival_time");

        builder.Property(x => x.TransportType)
            .HasColumnName("transport_type")
            .HasConversion<string>();

        builder.Property(x => x.DistanceKm)
            .HasColumnName("distance_km")
            .HasPrecision(5, 2)
            .HasConversion(
                v => v.Value,
                v => new DistanceKm(v))
            .IsRequired();

        builder.Property(x => x.IsDailyGoalAchieved)
            .HasColumnName("is_daily_goal_achieved");
    }
}
