using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.Infrastructure.Persistence.Analytics;

internal sealed class JourneyDistanceProjectionConfiguration
    : IEntityTypeConfiguration<JourneyDistanceProjection>
{
    public void Configure(EntityTypeBuilder<JourneyDistanceProjection> builder)
    {
        builder.ToTable("journey_distance_projection");
        builder.HasKey(x => x.JourneyId);

        builder.Property(x => x.JourneyId).HasColumnName("journey_id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Year).HasColumnName("year").IsRequired();
        builder.Property(x => x.Month).HasColumnName("month").IsRequired();
        builder.Property(x => x.DistanceKm).HasColumnName("distance_km").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Year, x.Month });
    }
}

internal sealed class MonthlyDistanceProjectionConfiguration
    : IEntityTypeConfiguration<MonthlyDistanceProjection>
{
    public void Configure(EntityTypeBuilder<MonthlyDistanceProjection> builder)
    {
        builder.ToTable("monthly_distance_projection");
        builder.HasKey(x => new { x.UserId, x.Year, x.Month });

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Year).HasColumnName("year").IsRequired();
        builder.Property(x => x.Month).HasColumnName("month").IsRequired();
        builder.Property(x => x.TotalDistanceKm).HasColumnName("total_distance_km").HasPrecision(14, 2).IsRequired();

        builder.HasIndex(x => new { x.Year, x.Month });
    }
}

internal sealed class AnalyticsInboxMessageConfiguration
    : IEntityTypeConfiguration<AnalyticsInboxMessage>
{
    public void Configure(EntityTypeBuilder<AnalyticsInboxMessage> builder)
    {
        builder.ToTable("analytics_inbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.OccurredUtc).HasColumnName("occurred_utc").IsRequired();
        builder.Property(x => x.ProcessedUtc).HasColumnName("processed_utc").IsRequired();
    }
}

