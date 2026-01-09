using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;

namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class NotificationDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<JourneyFavourite> JourneyFavourites => Set<JourneyFavourite>();

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }
}
