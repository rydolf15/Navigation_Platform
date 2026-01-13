using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NavigationPlatform.NotificationWorker.Persistence;

internal sealed class JourneyFavouriteConfiguration
    : IEntityTypeConfiguration<JourneyFavourite>
{
    public void Configure(EntityTypeBuilder<JourneyFavourite> builder)
    {
        builder.ToTable("journey_favourites");
        builder.HasKey(x => new { x.JourneyId, x.UserId });

        builder.Property(x => x.JourneyId).HasColumnName("journey_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
    }
}
