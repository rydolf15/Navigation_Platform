using NavigationPlatform.RewardWorker.Processing;

namespace NavigationPlatform.RewardWorker.Tests;

public sealed class DailyRewardEvaluatorTests
{
    [Theory]
    [InlineData(19.99, false)]
    [InlineData(20.00, true)]
    [InlineData(20.01, true)]
    public void Reward_threshold_is_correct(decimal totalKm, bool expected)
    {
        var result = DailyRewardEvaluator.ShouldGrant(totalKm);
        Assert.Equal(expected, result);
    }
}