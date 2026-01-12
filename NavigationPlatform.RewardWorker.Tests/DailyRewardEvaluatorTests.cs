using FluentAssertions;
using NavigationPlatform.RewardWorker.Processing;

namespace NavigationPlatform.RewardWorker.Tests;

public class DailyRewardEvaluatorTests
{
    [Theory]
    [InlineData(19.99, false)]
    [InlineData(20.00, true)]
    [InlineData(20.01, true)]
    public void ShouldGrant_WorksCorrectly(
        decimal total,
        bool expected)
    {
        DailyRewardEvaluator.ShouldGrant(total)
            .Should()
            .Be(expected);
    }
}
