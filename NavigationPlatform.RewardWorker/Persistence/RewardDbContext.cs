using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Common;
using NavigationPlatform.Infrastructure.Persistence.Outbox;

namespace NavigationPlatform.RewardWorker.Persistence;

internal sealed class RewardDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DailyDistanceProjection> Projections => Set<DailyDistanceProjection>();

    public RewardDbContext(DbContextOptions<RewardDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(NavigationPlatform.Infrastructure.Persistence.AppDbContext).Assembly);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(RewardDbContext).Assembly);
    }
}