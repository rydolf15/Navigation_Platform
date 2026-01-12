namespace NavigationPlatform.RewardWorker.Processing;

internal static class DailyRewardEvaluator
{
    public static bool ShouldGrant(decimal totalKm)
        => totalKm >= 20.00m;
}
