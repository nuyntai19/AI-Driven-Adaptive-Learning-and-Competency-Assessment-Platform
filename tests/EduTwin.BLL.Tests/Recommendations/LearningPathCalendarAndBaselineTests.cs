using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathCalendarAndBaselineTests
{
    [Theory]
    [InlineData(0, 0, "Inconclusive")]
    [InlineData(2, 2, "Inconclusive")]
    [InlineData(3, 3, "Inconclusive")]
    [InlineData(4, 4, "Inconclusive")]
    [InlineData(5, 4, "Strong")]
    [InlineData(5, 3, "Inconclusive")]
    [InlineData(5, 2, "NeedsSupport")]
    public void Baseline_uses_three_levels_and_never_confirms_a_tiny_sample(int answered, int correct, string expected)
        => Assert.Equal(expected, LearningAssessmentPolicy.Classify(answered, correct));

    [Theory]
    [InlineData(2, 2, "Inconclusive")]
    [InlineData(3, 3, "Strong")]
    [InlineData(3, 2, "Inconclusive")]
    [InlineData(3, 1, "NeedsSupport")]
    [InlineData(4, 3, "Inconclusive")]
    [InlineData(5, 4, "Strong")]
    [InlineData(5, 3, "Inconclusive")]
    [InlineData(5, 2, "NeedsSupport")]
    public void Placement_uses_80_and_40_percent_cut_offs(int answered, int correct, string expected)
        => Assert.Equal(expected, LearningAssessmentPolicy.ClassifyPlacement(answered, correct));

    [Fact]
    public void Real_days_exclusions_and_reserved_review_never_exceed_the_daily_limit()
    {
        var plan = new PathProposalDto { AvailableMinutesPerDay = 15, AvailableDaysPerWeek = 2,
            DeadlineDate = new DateTime(2026, 11, 9, 16, 59, 0, DateTimeKind.Utc),
            Schedule = new() { StudyDays = [1, 4], ExcludedDates = [new(2026, 10, 29)], ReviewReservePercent = 20 },
            Milestones = [new() { SkillIds = ["a"], Missions = [new() { MissionId = "lesson", SkillId = "a", Purpose = "Study", EstimatedMinutes = 30 },
                new() { MissionId = "review", SkillId = "a", Purpose = "Review", EstimatedMinutes = 10 }] }] };
        LearningPathCalendar.Schedule(plan, new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc));
        var slots = plan.Milestones.SelectMany(m => m.Missions).SelectMany(m => m.StudySlots).ToList();
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Contains((int)s.Date.DayOfWeek, new[] { 1, 4 }));
        Assert.DoesNotContain(slots, s => s.Date == new DateOnly(2026, 10, 29));
        Assert.All(slots.GroupBy(s => s.Date), g => Assert.True(g.Sum(s => s.Minutes) <= 15));
        Assert.True(plan.ScheduleSummary!.ReserveMinutes > 0);
        Assert.True(plan.Milestones[0].Missions[1].PlannedDate >= plan.Milestones[0].Missions[0].PlannedDate);
    }

    [Fact]
    public void Missing_prerequisite_capacity_defers_dependants_instead_of_teaching_them_first()
    {
        var plan = new PathProposalDto { AvailableMinutesPerDay = 10, AvailableDaysPerWeek = 7,
            DeadlineDate = new DateTime(2026, 10, 25, 16, 59, 0, DateTimeKind.Utc), Schedule = new() { ReviewReservePercent = 20 },
            Milestones = [new() { SkillIds = ["base"], Missions = [new() { MissionId = "base", SkillId = "base", Purpose = "Study", EstimatedMinutes = 20 }] },
                new() { SkillIds = ["target"], PrerequisiteSkillIds = ["base"], Missions = [new() { MissionId = "target", SkillId = "target", Purpose = "Study", EstimatedMinutes = 5 }] }] };
        LearningPathCalendar.Schedule(plan, new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(plan.Milestones[0].Missions[0].Deferred); // The affordable portion remains learnable.
        Assert.Equal(12u, plan.Milestones[0].Missions[0].RemainingUnscheduledMinutes);
        Assert.True(plan.Milestones[1].Missions[0].Deferred);
        Assert.Equal(17u, plan.ScheduleSummary!.MissingMinutes);
        Assert.Contains("target", plan.ScheduleSummary.DeferredSkillIds);
    }

    [Fact]
    public void Calendar_day_is_resolved_in_the_students_zone()
    {
        var instant = new DateTime(2026, 10, 25, 18, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 10, 26), LearningPathCalendar.LocalDate(instant, "Asia/Ho_Chi_Minh"));
        Assert.Equal(new DateOnly(2026, 10, 25), LearningPathCalendar.LocalDate(instant, "UTC"));
    }

    [Fact]
    public async Task Three_question_placement_finishes_without_ai_jobs_or_teacher_grading()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101 && q.QuestionId > 10103));
        await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English,
            GoalType = "ExamPrep", StartWithPlacement = true, TargetSkillIds = ["101"], Title = "Ôn thi", TargetScore = 8 });
        Assert.Equal("ExamPrep", goal.GoalType);
        Assert.True(goal.StartWithPlacement);
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        var mission = Assert.Single(Assert.Single(plan.Milestones).Missions);
        Assert.True(mission.CanStart); Assert.Equal(3u, mission.QuestionCount);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        foreach (var activity in run.Activities)
        {
            var question = await f.Db.Questions.SingleAsync(q => q.QuestionId == activity.Question!.Question!.QuestionId);
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, activity.ActivityRunId, new() {
                QuestionId = question.QuestionId, ClientSubmissionId = Guid.NewGuid(), FinalAnswer = question.CorrectAnswer! });
        }
        var evaluation = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("Inconclusive", evaluation.Outcome);
        Assert.All(await f.Db.AIAnalysisJobs.ToListAsync(), j => Assert.Equal(AIJobStatus.Completed, j.Status));
        Assert.All(await f.Db.EvidenceAssessments.ToListAsync(), e => Assert.False(e.RequiresTeacherReview));
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.PlacementReport!.IsComplete);
        Assert.Equal("Low", Assert.Single(agenda.PlacementReport.Skills).Confidence);
        Assert.Equal("Inconclusive", agenda.PlacementReport.Skills[0].Classification);
        var forecast = await LearningGoalForecast.ReadAsync(f.Db, f.Center, f.Student, f.English, null,
            DateTime.UtcNow, ["101"], default);
        Assert.Equal(10m, forecast.Score);
        Assert.Equal("Low", forecast.Confidence);
        var incomplete = await LearningGoalForecast.ReadAsync(f.Db, f.Center, f.Student, f.English, null,
            DateTime.UtcNow, ["101", "102"], default);
        Assert.Null(incomplete.Score);
        Assert.Equal("Insufficient", incomplete.Confidence);
    }

    [Fact]
    public async Task Empty_placement_pool_does_not_prevent_a_learning_route()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101)); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, StartWithPlacement = true, TargetSkillIds = ["101"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { GoalId = goal.GoalId, SubjectId = f.English });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.PlacementReport!.IsComplete);
        Assert.Equal("Unavailable", Assert.Single(agenda.PlacementReport.Skills).Status);
        var learning = await f.Proposal("101");
        Assert.Contains(learning.Milestones.SelectMany(m => m.Missions), m => m.CanStart && m.Purpose == "Study");
    }

    [Fact]
    public async Task Mock_exam_scores_actual_skills_and_follow_up_is_idempotent()
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalType = "ExamPrep",
            TargetSkillIds = ["101", "103"], TargetScore = 8, DeadlineDate = DateTime.UtcNow.AddDays(7), AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 7 });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        var mock = Assert.Single(plan.Milestones.SelectMany(m => m.Missions), m => m.IsMilestoneExam && m.IsProbe);
        Assert.False(mock.Deferred); Assert.Equal(20u, mock.TimeLimitMinutes);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mock.MissionId));
        Assert.NotNull(run.ExpiresAt); Assert.Equal(10, run.Activities.Count);
        foreach (var activity in run.Activities)
        {
            var qid = activity.Question!.Question!.QuestionId;
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, activity.ActivityRunId, new() { ClientSubmissionId = Guid.NewGuid(),
                QuestionId = qid, FinalAnswer = qid / 100 == 101 ? "wrong" : $"answer-{qid % 100}" });
        }
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Contains("101", result.SkillsNeedingSupport); Assert.DoesNotContain("101", result.ProvenSkills);
        Assert.Contains("103", result.ProvenSkills);
        var count = await f.Db.LearningPathSessions.CountAsync();
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal(count, await f.Db.LearningPathSessions.CountAsync());
        Assert.Single(await f.Db.PathDecisions.Where(d => d.TriggerType == $"ExamFollowUp:{run.SessionRunId}").ToListAsync());
        Assert.Contains(await f.Db.LearningPathSessions.ToListAsync(), s => s.TopicNodeId == 101 && s.Title.StartsWith("Rèn lại sau mốc thi"));
    }

    [Fact]
    public async Task Timed_exam_cannot_remain_in_progress_after_expiry()
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalType = "ExamPrep",
            TargetSkillIds = ["101"], DeadlineDate = DateTime.UtcNow.AddDays(7), AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 7 });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        var mock = plan.Milestones.SelectMany(m => m.Missions).Single(m => m.IsMilestoneExam && m.IsProbe);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mock.MissionId));
        var evaluator = new EduTwin.BLL.Recommendations.Services.LearningPathV4Evaluator(f.Db, new FutureClock());
        var result = await evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("TimedOut", result.Outcome);
        Assert.Equal(SessionRunStatus.Completed, (await f.Db.LearningPathSessionRuns.SingleAsync()).Status);
        Assert.Empty(await f.Db.Attempts.ToListAsync());
    }

    [Fact]
    public async Task Missed_study_date_creates_one_replacement_and_keeps_the_original_history()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await f.Proposal("101");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var path = await f.Db.LearningPaths.SingleAsync();
        var plan = System.Text.Json.JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json)!;
        var study = plan.Milestones.SelectMany(m => m.Missions).First(m => m.Purpose == "Study");
        var originalId = Guid.Parse(study.MissionId);
        study.PlannedDate = LearningPathCalendar.LocalDate(DateTime.UtcNow, plan.Schedule.TimeZoneId).AddDays(-1);
        path.PlanJson = System.Text.Json.JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json); await f.Db.SaveChangesAsync();
        await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(LearningPathSessionStatus.Skipped, (await f.Db.LearningPathSessions.SingleAsync(s => s.SessionId == originalId)).Status);
        var count = await f.Db.LearningPathSessions.CountAsync();
        await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(count, await f.Db.LearningPathSessions.CountAsync());
        Assert.Single(await f.Db.PathDecisions.Where(d => d.TriggerType == $"MissedStudyDate:{originalId}").ToListAsync());
    }

    private sealed class FutureClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddHours(1);
    }

    [Fact]
    public async Task Production_recommendation_boundary_never_calls_the_legacy_route_writer()
    {
        var history = new Moq.Mock<EduTwin.BLL.Recommendations.IRecommendationEngine>(Moq.MockBehavior.Strict);
        var engine = new EduTwin.BLL.Recommendations.V4RecommendationEngine(history.Object);
        var result = await engine.GenerateAndPersistAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateTime.UtcNow, default);
        Assert.Equal(EduTwin.BLL.Recommendations.RecommendationGenerationStatus.StaleIgnored, result.Status);
        history.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Two_first_previews_cannot_silently_replace_a_newly_accepted_goal()
    {
        using var f = new LearningPathV4Fixture();
        var first = await f.Proposal("101");
        var second = await f.Proposal("102");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, first.ProposalId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Planner.AcceptPathProposalAsync(f.Center, f.Student, second.ProposalId));
        Assert.Single(await f.Db.LearningPaths.ToListAsync());
    }

    [Fact]
    public async Task Twin_goal_edits_share_the_canonical_goal_and_invalidate_stale_previews()
    {
        using var f = new LearningPathV4Fixture();
        var now = DateTime.UtcNow;
        var twin = new EduTwin.DAL.DigitalTwin.StudentSubjectGoal { GoalId = 1, CenterId = f.Center, StudentId = f.Student,
            SubjectId = f.English, TargetScore = 8, RemainingDays = 30, CreatedAt = now, UpdatedAt = now, RowVersion = 1 };
        f.Db.StudentSubjectGoals.Add(twin);
        await LearningGoalBridge.StageFromTwinAsync(f.Db, twin, now, default); await f.Db.SaveChangesAsync();
        Assert.Equal(8m, (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!.TargetScore);
        var preview = await f.Proposal("101");
        twin.TargetScore = 9;
        await LearningGoalBridge.StageFromTwinAsync(f.Db, twin, now.AddMinutes(1), default); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Planner.AcceptPathProposalAsync(f.Center, f.Student, preview.ProposalId));
        Assert.Empty(await f.Db.LearningPaths.ToListAsync());
        Assert.Equal(9m, (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!.TargetScore);
    }
}
