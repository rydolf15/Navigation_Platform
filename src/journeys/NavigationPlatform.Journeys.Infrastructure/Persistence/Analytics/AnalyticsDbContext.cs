using Microsoft.EntityFrameworkCore;

namespace NavigationPlatform.Infrastructure.Persistence.Analytics;

public sealed class AnalyticsDbContext : DbContext
{
    public DbSet<JourneyDistanceProjection> JourneyDistances => Set<JourneyDistanceProjection>();
    public DbSet<MonthlyDistanceProjection> MonthlyDistances => Set<MonthlyDistanceProjection>();
    public DbSet<AnalyticsInboxMessage> InboxMessages => Set<AnalyticsInboxMessage>();

    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
    }
}

