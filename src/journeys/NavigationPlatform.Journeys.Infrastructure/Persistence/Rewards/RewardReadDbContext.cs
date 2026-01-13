using Microsoft.EntityFrameworkCore;

namespace NavigationPlatform.Infrastructure.Persistence.Rewards;

public sealed class RewardReadDbContext : DbContext
{
    public DbSet<DailyDistanceProjection> DailyDistances => Set<DailyDistanceProjection>();

    public RewardReadDbContext(DbContextOptions<RewardReadDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyDistanceProjection>(b =>
        {
            b.ToTable("daily_distance_projection");
            b.HasKey(x => new { x.UserId, x.Date });
            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.Date).HasColumnName("date");
            b.Property(x => x.TotalDistanceKm)
                .HasColumnName("total_distance_km")
                .HasPrecision(5, 2);
            b.Property(x => x.RewardGranted)
                .HasColumnName("reward_granted")
                .IsRequired();
        });
    }
}
