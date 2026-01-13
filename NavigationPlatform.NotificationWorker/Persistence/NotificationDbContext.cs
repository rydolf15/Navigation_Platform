using Microsoft.EntityFrameworkCore;

namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class NotificationDbContext : DbContext
{
    public DbSet<JourneyFavourite> JourneyFavourites => Set<JourneyFavourite>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
