namespace NavigationPlatform.Infrastructure.Persistence.Favourites;

public sealed class JourneyFavourite
{
    public Guid JourneyId { get; init; }
    public Guid UserId { get; init; }
}