namespace NavigationPlatform.NotificationWorker.Messaging;

internal static class NotificationEvents
{
    public const string JourneyUpdated = "JourneyUpdated";
    public const string JourneyDeleted = "JourneyDeleted";
    public const string JourneyShared = "JourneyShared";
    public const string JourneyUnshared = "JourneyUnshared";
    public const string JourneyFavouriteChanged = "JourneyFavouriteChanged";
    public const string JourneyFavorited = "JourneyFavorited";
    public const string JourneyDailyGoalAchieved = "JourneyDailyGoalAchieved";
}