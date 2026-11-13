using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningGoalEstimateTests
{
    private static CreateLearningGoalRequest Request(LearningPathV4Fixture f, DateTime? deadline = null, decimal? target = 8m) => new()
    {
        SubjectId = f.English, Title = "Mục tiêu tiếng Anh", GoalType = "ExamPrep", TargetSkillIds = ["101", "102", "103"],
        TargetScore = target, AvailableMinutesPerDay = 10, AvailableDaysPerWeek = 3, DeadlineDate = deadline,
        Schedule = new() { QuestionReuseDelayDays = 0 }
    };

    private static DateTime EndOfDay(DateOnly day, string zone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(23, 59, 59)), LearningPathCalendar.Zone(zone));

    [Fact]
    public async Task Estimate_WithoutDeadline_SuggestsADateTheRouteFits_AndSavesNothing()
    {
        using var f = new LearningPathV4Fixture();

        var estimate = await f.Planner.EstimateGoalAsync(f.Center, f.Student, Request(f));

        Assert.True(estimate.DeadlineSuggested);
        Assert.NotNull(estimate.DeadlineDate);
        Assert.NotNull(estimate.EarliestFeasibleDate);
        Assert.True(estimate.DeadlineDate >= estimate.EarliestFeasibleDate);
        Assert.NotEqual("InsufficientTime", estimate.FeasibilityStatus);
        Assert.Empty(await f.Db.LearningGoals.ToListAsync());
        Assert.Empty(await f.Db.PathProposals.ToListAsync());
    }

    [Fact]
    public async Task Estimate_DeadlineBeforeEarliestDate_WarnsWithAchievableScoreAndSuggestion()
    {
        using var f = new LearningPathV4Fixture();
        var zone = new LearningScheduleSettings().TimeZoneId;
        var suggestion = await f.Planner.EstimateGoalAsync(f.Center, f.Student, Request(f));
        var today = LearningPathCalendar.LocalDate(DateTime.UtcNow, zone);
        Assert.True(suggestion.EarliestFeasibleDate > today, "the fixture route must need more than one study day");

        var estimate = await f.Planner.EstimateGoalAsync(f.Center, f.Student, Request(f, EndOfDay(today, zone)));

        Assert.False(estimate.DeadlineSuggested);
        Assert.Equal(today, estimate.DeadlineDate);
        Assert.Equal("InsufficientTime", estimate.FeasibilityStatus);
        Assert.NotNull(estimate.SuggestedDeadline);
        Assert.True(estimate.SuggestedDeadline > today);
        Assert.False(string.IsNullOrWhiteSpace(estimate.FeasibilityMessage));
        Assert.Empty(await f.Db.LearningGoals.ToListAsync());
    }

    [Fact]
    public async Task Estimate_PastDeadline_IsRejectedLikeSaving()
    {
        using var f = new LearningPathV4Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Planner.EstimateGoalAsync(f.Center, f.Student, Request(f, DateTime.UtcNow.AddDays(-3))));
    }

    [Fact]
    public async Task Estimate_ForPlacement_IsRejected()
    {
        using var f = new LearningPathV4Fixture();
        var request = Request(f); request.StartWithPlacement = true;

        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.EstimateGoalAsync(f.Center, f.Student, request));
    }
}
