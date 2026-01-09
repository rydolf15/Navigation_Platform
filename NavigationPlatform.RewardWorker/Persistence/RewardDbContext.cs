using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Infrastructure.Persistence.Outbox;

namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class RewardDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DailyDistanceProjection> Projections => Set<DailyDistanceProjection>();

    public RewardDbContext(DbContextOptions<RewardDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(RewardDbContext).Assembly);
}