using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Domain.Common;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;

namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class NotificationDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<JourneyFavourite> JourneyFavourites => Set<JourneyFavourite>();

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(NavigationPlatform.Infrastructure.Persistence.AppDbContext).Assembly);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(NotificationDbContext).Assembly);
    }
}
