using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NavigationPlatform.Domain.Journeys;
using NavigationPlatform.Infrastructure.Persistence.Audit;
using NavigationPlatform.Infrastructure.Persistence.Favourites;
using NavigationPlatform.Infrastructure.Persistence.Outbox;
using NavigationPlatform.Infrastructure.Persistence.Sharing;

namespace NavigationPlatform.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<Journey> Journeys => Set<Journey>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

}

internal sealed class JourneyFavouriteConfiguration
   : IEntityTypeConfiguration<JourneyFavourite>
{
    public void Configure(EntityTypeBuilder<JourneyFavourite> builder)
    {
        builder.ToTable("journey_favourites");
        builder.HasKey(x => new { x.JourneyId, x.UserId });
    }
}

internal sealed class JourneyShareConfiguration
   : IEntityTypeConfiguration<JourneyShare>
{
    public void Configure(EntityTypeBuilder<JourneyShare> builder)
    {
        builder.ToTable("journey_shares");
        builder.HasKey(x => new { x.JourneyId, x.SharedWithUserId });
    }
}

internal sealed class JourneyPublicLinkConfiguration
    : IEntityTypeConfiguration<JourneyPublicLink>
{
    public void Configure(EntityTypeBuilder<JourneyPublicLink> builder)
    {
        builder.ToTable("journey_public_links");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Id).IsUnique();
    }
}

internal sealed class JourneyShareAuditConfiguration
    : IEntityTypeConfiguration<JourneyShareAudit>
{
    public void Configure(EntityTypeBuilder<JourneyShareAudit> builder)
    {
        builder.ToTable("journey_share_audits");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Id).IsUnique();
    }
}
