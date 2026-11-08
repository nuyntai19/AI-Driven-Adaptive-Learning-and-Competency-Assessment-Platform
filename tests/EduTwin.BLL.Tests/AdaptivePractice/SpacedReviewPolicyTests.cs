using System;
using EduTwin.BLL.AdaptivePractice;
using Xunit;

namespace EduTwin.BLL.Tests.AdaptivePractice;

public sealed class SpacedReviewPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static SpacedReviewPolicy.Candidate Studied(ulong id, decimal mastery, double daysAgo) =>
        new(id, mastery, Now.AddDays(-daysAgo));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(49.9, 1)]
    [InlineData(50, 3)]
    [InlineData(69.9, 3)]
    [InlineData(70, 7)]
    [InlineData(84.9, 7)]
    [InlineData(85, 14)]
    public void WeakerTopicsComeBackSooner(double mastery, int expectedDays) =>
        Assert.Equal(expectedDays, SpacedReviewPolicy.IntervalDays((decimal)mastery));

    [Fact]
    public void NothingStudied_ReturnsNull() =>
        Assert.Null(SpacedReviewPolicy.Choose([], Now));

    [Fact]
    public void PicksTheTopicFurthestPastItsInterval()
    {
        // 90% after 15 days is 1.07 intervals; 60% after 6 days is 2 intervals.
        var choice = SpacedReviewPolicy.Choose([Studied(1, 90m, 15), Studied(2, 60m, 6)], Now);

        Assert.Equal(2ul, choice!.TopicId);
        Assert.True(choice.IsDue);
        Assert.Equal(6, choice.DaysSinceEvidence);
    }

    [Fact]
    public void WhenNothingIsDue_ReportsTheClosestTopicAndItsWait()
    {
        var choice = SpacedReviewPolicy.Choose([Studied(1, 90m, 1), Studied(2, 60m, 1)], Now);

        Assert.Equal(2ul, choice!.TopicId);
        Assert.False(choice.IsDue);
        Assert.Equal(2, choice.DaysUntilDue);
    }
}
