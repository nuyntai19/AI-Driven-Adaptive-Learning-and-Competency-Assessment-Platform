using EduTwin.BLL.Assignments;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathHandoffTests
{
    [Fact]
    public async Task Prerequisite_probes_cover_different_skills_and_have_visible_labels()
    {
        using var f = new LearningPathV4Fixture();
        f.AddPrerequisite(101, 103); f.AddPrerequisite(102, 103); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, StartWithPlacement = true, TargetSkillIds = ["103"] });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var mission = plan.Milestones.Single(m => m.SkillIds.Contains("103")).Missions[0];
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var first = run.Activities[0];
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, first.ActivityRunId, new() {
            ClientSubmissionId = Guid.NewGuid(), QuestionId = first.Question!.Question!.QuestionId, FinalAnswer = "wrong" });
        var updated = await f.Execution.GetMissionRunAsync(f.Center, f.Student, Guid.Parse(mission.MissionId), run.SessionRunId);
        var foundations = updated.Activities.Skip(5).ToList();
        Assert.Equal(2, foundations.Count);
        Assert.All(foundations, a => Assert.Contains("Kiểm tra nền tảng", a.Stage));
        var ids = foundations.Select(a => a.Question!.Question!.QuestionId).ToList();
        Assert.Equal(2, await f.Db.Questions.Where(q => ids.Contains(q.QuestionId)).Select(q => q.PrimaryTopicNodeId).Distinct().CountAsync());
    }

    [Fact]
    public async Task Used_questions_wait_before_practice_and_never_become_fresh_check_evidence()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 102 || q.PrimaryTopicNodeId == 103)); await f.Db.SaveChangesAsync();
        for (ulong i = 1; i <= 12; i++) await f.RecordEvidence(101, i, false);
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, TargetSkillIds = ["101"] });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var practice = plan.Milestones.SelectMany(m => m.Missions).First(m => m.Purpose == "Practice");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(practice.MissionId)));
        foreach (var attempt in await f.Db.Attempts.ToListAsync()) attempt.CreatedAt = DateTime.UtcNow.AddDays(-3);
        await f.Db.SaveChangesAsync();
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.Missions.Single(m => m.MissionId == practice.MissionId).CanStart);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(practice.MissionId));
        Assert.NotEmpty(run.Activities);
        await f.Execution.AbandonMissionRunAsync(f.Center, f.Student, Guid.Parse(practice.MissionId), run.SessionRunId);
        var check = plan.Milestones.SelectMany(m => m.Missions).Single(m => m.Purpose == "IndependentCheck");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId)));
    }

    [Fact]
    public async Task Skipping_an_ungradable_answer_finishes_placement_without_claiming_strength()
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, StartWithPlacement = true, TargetSkillIds = ["101"] });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(plan.Milestones[0].Missions[0].MissionId));
        foreach (var a in run.Activities)
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new() {
                ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, Skipped = true });
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("Inconclusive", result.Outcome);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.PlacementReport!.IsComplete);
        Assert.Equal("Inconclusive", Assert.Single(agenda.PlacementReport.Skills).Classification);
    }

    [Fact]
    public async Task Placement_baseline_changes_learning_order_and_schedules_inconclusive_rechecks()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101 && q.QuestionId > 10103)); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, StartWithPlacement = true,
            TargetSkillIds = ["101", "102", "103"], AvailableMinutesPerDay = 120, Schedule = new() { QuestionReuseDelayDays = 0 } });
        var baseline = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, baseline.ProposalId);
        foreach (var mission in baseline.Milestones.SelectMany(m => m.Missions))
        {
            var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
            await Submit(f, run, mission.SkillId == "101" ? 3 : mission.SkillId == "102" ? 1 : 3);
            await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        }
        var plan = await f.Proposal("101", "102", "103");
        Assert.Equal("102", plan.Milestones[0].SkillIds[0]);
        Assert.DoesNotContain(plan.Milestones.Single(m => m.SkillIds.Contains("101")).Missions, m => m.Purpose == "Study");
        var recheck = Assert.Single(plan.Milestones.Single(m => m.SkillIds.Contains("103")).Missions, m => m.Purpose == "IndependentCheck");
        Assert.True(recheck.IsProbe); Assert.True(recheck.AvailableFrom > DateTime.UtcNow.AddDays(1));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(9, 4)]
    public async Task Failed_stage_holds_only_dependants_and_repair_then_retake_opens_them(int targetScore, int correct)
    {
        using var f = new LearningPathV4Fixture();
        f.AddPrerequisite(101, 102); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalType = "ExamPrep", TargetScore = targetScore,
            TargetSkillIds = ["101", "102", "103"], AvailableMinutesPerDay = 120, Schedule = new() { QuestionReuseDelayDays = 0 } });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var stage = plan.Milestones[0].Missions.Single(m => m.IsMilestoneExam && !m.IsProbe);
        // Seed completed learning; the test exercises the real timed assessment, repair and retry flow.
        foreach (var session in await f.Db.LearningPathSessions.Where(s => s.TopicNodeId == 101 && s.Objective != "IndependentCheck").ToListAsync())
            session.Status = LearningPathSessionStatus.Completed;
        await f.Db.SaveChangesAsync();
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(stage.MissionId));
        await Submit(f, run, correct);
        var failed = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("NeedsSupport", failed.Outcome); // Both the 80% floor and a higher target keep their retry path.
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.True(map.Skills.Single(s => s.SkillId == "102").BlockedByStageGate);
        Assert.False(map.Skills.Single(s => s.SkillId == "103").BlockedByStageGate);
        var child = plan.Milestones.Single(m => m.SkillIds.Contains("102")).Missions.First(m => m.Purpose == "Practice");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(child.MissionId)));
        for (var i = 0; i < 2; i++)
        {
            var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 120);
            var practice = agenda.Missions.Last(m => m.SkillId == "101" && m.Purpose == "Practice" && m.Status == "NotStarted");
            var repair = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(practice.MissionId));
            await Submit(f, repair, int.MaxValue);
            await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, repair.SessionRunId);
        }
        var ready = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 120);
        var check = ready.Missions.Last(m => m.SkillId == "101" && m.IsMilestoneExam && m.MissionId != stage.MissionId);
        var retry = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId));
        await Submit(f, retry, int.MaxValue);
        Assert.Equal("Passed", (await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, retry.SessionRunId)).Outcome);
        Assert.False((await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English)).Skills.Single(s => s.SkillId == "102").BlockedByStageGate);
    }

    [Theory]
    [InlineData("Gentle", 1u)]
    [InlineData("Moderate", 3u)]
    [InlineData("Accelerated", 3u)]
    public async Task Pace_changes_daily_sets_within_the_same_time_budget(string pace, uint sets)
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, TargetSkillIds = ["101"], PreferredPace = pace, AvailableMinutesPerDay = 120 });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 120);
        Assert.Equal(sets, Assert.Single(agenda.TodayPlan, m => m.Purpose == "Practice").PlannedSets);
        Assert.True(agenda.PlannedMinutes <= 120);
    }

    private static async Task Submit(LearningPathV4Fixture f, MissionRunDto run, int correct)
    {
        var index = 0;
        foreach (var activity in run.Activities)
        {
            var id = activity.Question!.Question!.QuestionId;
            var question = await f.Db.Questions.SingleAsync(q => q.QuestionId == id);
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, activity.ActivityRunId, new() {
                QuestionId = id, ClientSubmissionId = Guid.NewGuid(), FinalAnswer = index++ < correct ? question.CorrectAnswer! : "wrong" });
        }
        foreach (var a in await f.Db.Attempts.Where(a => a.Status != EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.Completed).ToListAsync())
        { a.Status = EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.Completed; await f.Assess(a); }
        foreach (var job in await f.Db.AIAnalysisJobs.ToListAsync()) job.Status = EduTwin.Contracts.AssessmentAndReasoning.AIJobStatus.Completed;
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public void Mock_is_scheduled_backwards_and_leaves_time_for_repair()
    {
        var mock = new ProposedMissionDto { MissionId = "mock", SkillId = "101", IsMilestoneExam = true, IsProbe = true, EstimatedMinutes = 10 };
        var plan = new PathProposalDto { AvailableMinutesPerDay = 20, AvailableDaysPerWeek = 7,
            DeadlineDate = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc), Schedule = new() { TimeZoneId = "UTC" },
            Milestones = [new() { SkillIds = ["101"], Missions = [new() { MissionId = "study", SkillId = "101", EstimatedMinutes = 10 }, mock] }] };
        LearningPathCalendar.Schedule(plan, new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateOnly(2026, 11, 3), mock.PlannedDate);
        Assert.All(plan.Milestones.SelectMany(m => m.Missions).SelectMany(m => m.StudySlots).GroupBy(s => s.Date), g => Assert.True(g.Sum(s => s.Minutes) <= 20));
    }

    [Fact]
    public void Budget_advice_uses_the_actual_excluded_days()
    {
        var today = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);
        var plan = new PathProposalDto { AvailableMinutesPerDay = 10, AvailableDaysPerWeek = 7, DeadlineDate = today,
            Schedule = new() { TimeZoneId = "UTC", ReviewReservePercent = 0, ExcludedDates = [new(2026, 10, 27)] },
            Milestones = [new() { SkillIds = ["101"], Missions = [new() { MissionId = "study", SkillId = "101", EstimatedMinutes = 30 }] }] };
        LearningPathCalendar.Schedule(plan, today);
        Assert.Contains("30 phút/ngày", plan.ScheduleSummary!.Message);
        Assert.Contains("29/10/2026", plan.ScheduleSummary.Message);
        Assert.Equal(20u, plan.ScheduleSummary.MissingMinutes);
    }

    [Fact]
    public async Task Past_deadline_is_rejected_in_the_students_time_zone()
    {
        using var f = new LearningPathV4Fixture();
        var planner = new LearningPathV4Planner(f.Db, new FixedClock(new(2026, 10, 26, 1, 0, 0, TimeSpan.Zero)));
        await Assert.ThrowsAsync<ArgumentException>(() => planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() {
            SubjectId = f.English, DeadlineDate = new DateTime(2026, 10, 25, 16, 59, 0, DateTimeKind.Utc),
            Schedule = new() { TimeZoneId = "Asia/Ho_Chi_Minh" } }));
        Assert.Empty(await f.Db.LearningGoals.ToListAsync());
    }

    [Fact]
    public async Task Reading_agenda_closes_an_expired_run_and_releases_unanswered_reservations()
    {
        using var f = new LearningPathV4Fixture();
        var plan = await ExamProposal(f, DateTime.UtcNow.AddDays(7));
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var mock = plan.Milestones.SelectMany(m => m.Missions).Single(m => m.IsMilestoneExam && m.IsProbe);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mock.MissionId));
        var future = new FixedClock(DateTimeOffset.UtcNow.AddHours(1));
        var agenda = await new LearningPathV4Planner(f.Db, future).GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Empty(agenda.OngoingRuns);
        Assert.Equal(SessionRunStatus.Completed, (await f.Db.LearningPathSessionRuns.SingleAsync(r => r.SessionRunId == run.SessionRunId)).Status);
        Assert.False(await f.Db.IssuedItems.AnyAsync(i => i.ActivityRun.SessionRunId == run.SessionRunId && i.ActivityRun.SessionRun.Status == SessionRunStatus.InProgress));
        Assert.Empty(await f.Db.Attempts.ToListAsync());
        var count = await f.Db.LearningPathSessions.CountAsync();
        await new LearningPathV4Planner(f.Db, future).GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(count, await f.Db.LearningPathSessions.CountAsync());
    }

    [Fact]
    public async Task Deadline_change_regenerates_unstarted_mocks_without_deleting_history()
    {
        using var f = new LearningPathV4Fixture();
        var plan = await ExamProposal(f, DateTime.UtcNow.AddDays(7));
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var old = plan.Milestones.SelectMany(m => m.Missions).Single(m => m.IsMilestoneExam && m.IsProbe);
        var goal = await f.Db.LearningGoals.SingleAsync(g => g.GoalId == plan.GoalId);
        goal.DeadlineDate = DateTime.UtcNow.AddDays(12); goal.RowVersion++; await f.Db.SaveChangesAsync();
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal("Skipped", agenda.Missions.Single(m => m.MissionId == old.MissionId).Status);
        var replacement = Assert.Single(agenda.Missions, m => m.IsMockExam && m.Status != "Skipped");
        Assert.NotEqual(old.MissionId, replacement.MissionId);
        var count = await f.Db.LearningPathSessions.CountAsync();
        await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(count, await f.Db.LearningPathSessions.CountAsync());
    }

    [Fact]
    public async Task Stage_exam_has_no_deadline_dependency_and_cannot_start_before_study()
    {
        using var f = new LearningPathV4Fixture();
        var plan = await ExamProposal(f, null);
        var stage = Assert.Single(plan.Milestones.SelectMany(m => m.Missions), m => m.IsMilestoneExam && !m.IsProbe);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, plan.ProposalId);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(stage.MissionId)));
        Assert.Contains("hoàn thành", error.Message);
    }

    [Fact]
    public async Task Exam_importance_orders_independent_skills_but_preserves_prerequisites()
    {
        using var f = new LearningPathV4Fixture();
        (await f.Db.KnowledgeNodes.SingleAsync(n => n.NodeId == 101)).ExamImportance = 10;
        (await f.Db.KnowledgeNodes.SingleAsync(n => n.NodeId == 102)).ExamImportance = 100;
        (await f.Db.KnowledgeNodes.SingleAsync(n => n.NodeId == 103)).ExamImportance = 80;
        f.AddPrerequisite(101, 102); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalType = "ExamPrep", TargetSkillIds = ["101", "102", "103"] });
        var plan = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        Assert.Equal(new[] { "101", "102", "103" }, plan.Milestones.SelectMany(m => m.SkillIds));
    }

    [Theory]
    [InlineData(AssignmentKind.Midterm)]
    [InlineData(AssignmentKind.Final)]
    public async Task Late_enrollment_sees_live_exam_schedule_and_cancellation(AssignmentKind kind)
    {
        using var f = new LearningPathV4Fixture();
        var classId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        f.Db.Classes.Add(new Class { CenterId = f.Center, ClassId = classId, SubjectId = f.English, ClassName = "Test", AcademicYear = "2026", GradeLevel = 10,
            Status = EduTwin.Contracts.Organization.ClassStatus.Active, CreatedAt = now, UpdatedAt = now });
        f.Db.ClassStudents.Add(new ClassStudent { CenterId = f.Center, ClassId = classId, StudentId = f.Student,
            Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, JoinedAt = now });
        var assignment = new Assignment { CenterId = f.Center, AssignmentId = Guid.NewGuid(), ClassId = classId, Title = "Exam", Kind = kind,
            Status = AssignmentStatus.Published, ExamAt = now.AddDays(4), TimeLimitMinutes = 30, TargetMode = TargetSource.WholeClass,
            CreatedAt = now, UpdatedAt = now };
        f.Db.Assignments.Add(assignment); await f.Db.SaveChangesAsync();
        await ClassExamEnrollment.StageAsync(f.Db, f.Center, classId, new[] { f.Student }, now, default); await f.Db.SaveChangesAsync();
        Assert.Single(await f.Db.AssignmentTargets.ToListAsync());
        Assert.Single(await f.Db.StudentAssignmentProgresses.ToListAsync());
        await ClassExamEnrollment.StageAsync(f.Db, f.Center, classId, new[] { f.Student }, now, default); await f.Db.SaveChangesAsync();
        Assert.Single(await f.Db.StudentAssignmentProgresses.ToListAsync());
        async Task<List<ExamMilestoneDto>> Exams() => (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Exams;
        var curriculum = new EduTwin.DAL.CurriculumAndQuestions.Curriculum { CenterId = f.Center, CurriculumId = Guid.NewGuid(), SubjectId = f.English,
            GraphVersionId = f.EnglishGraphVersionId, GradeLevel = 10, Title = "Test", ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        f.Db.Curriculums.Add(curriculum);
        f.Db.CurriculumNodes.Add(new() { CenterId = f.Center, CurriculumId = curriculum.CurriculumId, NodeId = 101, OrderIndex = 1, CreatedAt = now });
        f.Db.ClassCurriculumApplications.Add(new() { CenterId = f.Center, ApplicationId = Guid.NewGuid(), ClassId = classId,
            CurriculumId = curriculum.CurriculumId, SubjectId = f.English, StartedAt = now }); await f.Db.SaveChangesAsync();
        // Historical late enrollment may predate the enrollment hook; agenda repairs the missing links.
        f.Db.AssignmentTargets.RemoveRange(f.Db.AssignmentTargets);
        f.Db.StudentAssignmentProgresses.RemoveRange(f.Db.StudentAssignmentProgresses); await f.Db.SaveChangesAsync();
        Assert.Equal(assignment.ExamAt, Assert.Single(await Exams()).ExamAt);
        Assert.Single(await f.Db.AssignmentTargets.ToListAsync());
        Assert.Single(await f.Db.StudentAssignmentProgresses.ToListAsync());
        assignment.ExamAt = now.AddDays(6); await f.Db.SaveChangesAsync();
        Assert.Equal(assignment.ExamAt, Assert.Single(await Exams()).ExamAt);
        assignment.Status = AssignmentStatus.Closed; await f.Db.SaveChangesAsync();
        Assert.Equal("Cancelled", Assert.Single(await Exams()).Status);
        var later = await new LearningPathV4Planner(f.Db, new FixedClock(new DateTimeOffset(now.AddDays(10), TimeSpan.Zero)))
            .GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal("Cancelled", Assert.Single(later.Exams).Status);
    }

    private static async Task<PathProposalDto> ExamProposal(LearningPathV4Fixture f, DateTime? deadline)
    {
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalType = "ExamPrep", TargetSkillIds = ["101"],
            TargetScore = 5, DeadlineDate = deadline, AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 7 });
        return await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
