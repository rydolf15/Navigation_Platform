using Microsoft.EntityFrameworkCore;
using NavigationPlatform.RewardWorker.Persistence.Outbox;

namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class RewardDbContext : DbContext
{
    public DbSet<DailyDistanceProjection> DailyDistances => Set<DailyDistanceProjection>();
    public DbSet<JourneyProjection> Journeys => Set<JourneyProjection>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public RewardDbContext(DbContextOptions<RewardDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RewardDbContext).Assembly);
    }
}