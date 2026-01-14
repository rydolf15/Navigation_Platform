using FluentAssertions;
using NavigationPlatform.RewardWorker.Processing;

namespace NavigationPlatform.RewardWorker.Tests;

public class DailyRewardEvaluatorTests
{
    [Theory]
    // Use hundredths to avoid double->decimal precision issues in InlineData.
    [InlineData(1999, false)]
    [InlineData(2000, true)]
    [InlineData(2001, true)]
    public void ShouldGrant_WorksCorrectly(
        int totalHundredths,
        bool expected)
    {
        var total = totalHundredths / 100m;

        DailyRewardEvaluator.ShouldGrant(total)
            .Should()
            .Be(expected);
    }
}
