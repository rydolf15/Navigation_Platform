using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NavigationPlatform.Infrastructure.Persistence.Favourites;

namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class JourneyFavouriteConfiguration
    : IEntityTypeConfiguration<JourneyFavourite>
{
    public void Configure(EntityTypeBuilder<JourneyFavourite> builder)
    {
        builder.ToTable("journey_favourites");
        builder.HasKey(x => new { x.JourneyId, x.UserId });
    }
}
