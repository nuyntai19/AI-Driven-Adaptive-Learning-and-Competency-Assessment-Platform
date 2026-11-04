using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathFeasibilityTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 2, 0, 0, DateTimeKind.Utc);

    private static PathProposalDto BuildPlan(decimal? targetScore, DateTime? deadline)
    {
        var plan = new PathProposalDto
        {
            TargetScore = targetScore, DeadlineDate = deadline,
            AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 5, PracticeQuestionsPerSet = 6,
            Schedule = new() { TimeZoneId = "Asia/Ho_Chi_Minh", StudyDays = [1, 2, 3, 4, 5], ReviewReservePercent = 20 },
            Milestones = [new()
            {
                SkillIds = ["a"],
                Missions = [new() { MissionId = "m1", SkillId = "a", Purpose = "Practice", EstimatedMinutes = 600, EstimatedSets = 1, SetNumber = 1, QuestionCount = 6 }]
            }]
        };
        LearningPathCalendar.Schedule(plan, Now);
        return plan;
    }

    [Fact]
    public void Too_short_deadline_is_infeasible_with_achievable_score()
    {
        var plan = BuildPlan(9m, new DateTime(2026, 11, 5, 16, 59, 0, DateTimeKind.Utc));
        LearningPathFeasibility.Annotate(plan, 3m, "Forecast", "5/5", Now);
        Assert.Equal("InsufficientTime", plan.FeasibilityStatus);
        Assert.Equal(750u, plan.RequiredStudyMinutes);
        Assert.Equal(25u, plan.MinimumStudyDays);
        Assert.NotNull(plan.AchievableScore);
        Assert.InRange(plan.AchievableScore!.Value, 3.1m, 8.9m);
        Assert.Contains("Không có lộ trình phù hợp", plan.FeasibilityMessage);
    }

    [Fact]
    public void Generous_deadline_is_feasible()
    {
        var plan = BuildPlan(9m, new DateTime(2027, 6, 1, 16, 59, 0, DateTimeKind.Utc));
        LearningPathFeasibility.Annotate(plan, 3m, "Forecast", "5/5", Now);
        Assert.Equal("Feasible", plan.FeasibilityStatus);
    }

    [Fact]
    public void Earliest_deadline_is_tight()
    {
        var probe = BuildPlan(9m, null);
        LearningPathFeasibility.Annotate(probe, 3m, "Forecast", "5/5", Now);
        Assert.NotNull(probe.EarliestFeasibleDate);
        Assert.NotNull(probe.SuggestedDeadline);
        Assert.True(probe.SuggestedDeadline > probe.EarliestFeasibleDate);

        var plan = BuildPlan(9m, probe.EarliestFeasibleDate);
        LearningPathFeasibility.Annotate(plan, 3m, "Forecast", "5/5", Now);
        Assert.Equal("TightBudget", plan.FeasibilityStatus);
    }

    [Fact]
    public void No_deadline_suggests_dates()
    {
        var plan = BuildPlan(9m, null);
        LearningPathFeasibility.Annotate(plan, 3m, "Forecast", "5/5", Now);
        Assert.Equal("Feasible", plan.FeasibilityStatus);
        Assert.Equal(750u, plan.RequiredStudyMinutes);
        Assert.NotNull(plan.EarliestFeasibleDate);
        Assert.NotNull(plan.SuggestedDeadline);
    }

    [Theory]
    [InlineData(9.0, 30.0, 3)]
    [InlineData(7.0, 60.0, 1)]
    [InlineData(6.0, 80.0, 0)]
    [InlineData(9.0, null, 2)]
    public void Extra_sets_follow_target_gap(double target, double? mastery, int expected)
        => Assert.Equal((uint)expected, LearningPathPacing.ExtraSetsForTargetGap((decimal)target, mastery.HasValue ? (decimal)mastery.Value : null));

    [Fact]
    public void Extra_sets_are_zero_without_a_target()
        => Assert.Equal(0u, LearningPathPacing.ExtraSetsForTargetGap(null, 30m));

    [Fact]
    public void Mastery_map_average_is_weighted()
    {
        var skills = new[]
        {
            new SkillCapabilityStatusDto { SkillId = "a", MasteryScore = 80m, EvidenceCount = 3 },
            new SkillCapabilityStatusDto { SkillId = "b", MasteryScore = 40m, EvidenceCount = 2 },
            new SkillCapabilityStatusDto { SkillId = "c", MasteryScore = 90m, EvidenceCount = 0 }
        };
        var weights = new Dictionary<string, decimal> { ["a"] = 50m, ["b"] = 50m, ["c"] = 50m };
        var (score, covered, total) = LearningPathFeasibility.FromMasteryMap(skills, weights);
        Assert.Equal(6.0m, score);
        Assert.Equal(2, covered);
        Assert.Equal(3, total);
    }
}
