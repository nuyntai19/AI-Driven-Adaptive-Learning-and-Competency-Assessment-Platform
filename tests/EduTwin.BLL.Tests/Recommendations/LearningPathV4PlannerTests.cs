using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathV4PlannerTests
{
    [Fact]
    public async Task AcceptedMigration_ClosesOnlyUnfinishedLegacyExecution_WithoutDeletingSubmissions()
    {
        using var f = new LearningPathV4Fixture(); var now = DateTime.UtcNow;
        var old = new EduTwin.DAL.Recommendations.LearningPath { CenterId = f.Center, StudentId = f.Student, SubjectId = f.English,
            LearningPathId = Guid.NewGuid(), Status = LearningPathStatus.Active, PlanSchemaVersion = "2.0", Version = 1,
            GeneratedAt = now, CreatedAt = now, UpdatedAt = now };
        var session = new EduTwin.DAL.Recommendations.LearningPathSession { CenterId = f.Center, LearningPath = old, SessionId = Guid.NewGuid(),
            TopicNodeId = 101, Title = "Legacy", Objective = "Legacy", Status = LearningPathSessionStatus.InProgress, CreatedAt = now, UpdatedAt = now };
        var run = new EduTwin.DAL.Recommendations.LearningPathSessionRun { CenterId = f.Center, StudentId = f.Student, Session = session,
            Status = SessionRunStatus.InProgress, StartedAt = now, LastActivityAt = now, CreatedAt = now, UpdatedAt = now };
        f.Db.LearningPaths.Add(old); f.Db.LearningPathSessions.Add(session); f.Db.LearningPathSessionRuns.Add(run); await f.Db.SaveChangesAsync();
        var mission = await f.Activate("Study", "101");
        Assert.Equal(SessionRunStatus.Abandoned, run.Status); Assert.Equal(SessionRunOutcome.None, run.Outcome);
        Assert.Equal(LearningPathStatus.Superseded, old.Status);
        Assert.NotNull(await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId)));
    }
    [Fact]
    public async Task ExamPreparation_OffersARealProbeFirst_WithoutInventingConfirmedSkills()
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest { SubjectId = f.English,
            GoalType = "ExamPrep", TargetSkillIds = ["103"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        Assert.Equal("IndependentCheck", proposal.InitialMission!.Purpose); Assert.Empty(proposal.ConfirmedSkills);
    }
    [Fact]
    public async Task Map_UsesSubjectLeaves_NotParentOrHardcodedMath()
    {
        using var f = new LearningPathV4Fixture();
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.Equal(new[] { "101", "102", "103" }, map.Skills.Select(s => s.SkillId));
        Assert.DoesNotContain(map.Skills, s => s.SkillName == "Thì" || s.SkillName.Contains("Phương trình"));
        Assert.All(map.Skills, s => { Assert.Equal("Unknown", s.State); Assert.Null(s.MasteryScore); Assert.Equal(0u, s.EvidenceCount); });
    }
    [Fact]
    public async Task WrongAnswerInAnotherSubject_DoesNotInventEnglishMisconceptions()
    {
        using var f = new LearningPathV4Fixture();
        await f.RecordEvidence(201, 1, correct: false);
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.All(map.Skills, s => Assert.Equal("Unknown", s.State));
    }
    [Fact]
    public async Task NewConfirmedResults_ChangeNextMission_OnlyForThatSkill()
    {
        using var f = new LearningPathV4Fixture();
        await f.Activate("Study");
        var before = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal("101", before.PrimaryMission!.SkillId);
        Assert.Equal("Study", before.PrimaryMission.Purpose);
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i);
        var after = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.DoesNotContain(after.AlternativeOptions.Prepend(after.PrimaryMission!).Where(m => m != null), m => m.SkillId == "101" && m.Purpose == "Study");
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.Equal("Confirmed", map.Skills.Single(s => s.SkillId == "101").State);
        Assert.Equal("Unknown", map.Skills.Single(s => s.SkillId == "102").State);
    }
    [Fact]
    public async Task PendingReviewAndGuidedAnswers_DoNotConfirmSkill()
    {
        using var f = new LearningPathV4Fixture();
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i, review: true);
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(102, i, postFeedback: true);
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.DoesNotContain(map.Skills, s => s.State == "Confirmed");
    }
    [Fact]
    public async Task GoalFiltersRoute_AndPreviewDoesNotReplaceAcceptedGoal()
    {
        using var f = new LearningPathV4Fixture();
        var first = await f.Proposal("101"); await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, first.ProposalId);
        var second = await f.Proposal("103");
        Assert.All(second.Milestones, m => Assert.Contains("103", m.SkillIds));
        Assert.Equal(first.GoalId, (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!.GoalId);
    }
    [Fact]
    public async Task Accept_IsIdempotent_CreatesAllMissionsWithRealTopicForeignKeys()
    {
        using var f = new LearningPathV4Fixture();
        var p = await f.Proposal(); var a = await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, p.ProposalId);
        var b = await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, p.ProposalId);
        Assert.Equal(a.LearningPathId, b.LearningPathId);
        Assert.Equal(p.Milestones.Sum(m => m.Missions.Count), await f.Db.LearningPathSessions.CountAsync());
        Assert.All(await f.Db.LearningPathSessions.ToListAsync(), s => Assert.Contains(s.TopicNodeId, new ulong[] { 101, 102, 103 }));
    }
    [Fact]
    public async Task AgendaAndMap_AreReadOnly_AndDoNotRelabelLongWorkAsFiveMinutes()
    {
        using var f = new LearningPathV4Fixture(); await f.Activate("Study");
        var five = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 5);
        Assert.All(five.AlternativeOptions.Prepend(five.PrimaryMission!).Where(m => m != null), m => Assert.True(m.EstimatedDurationMinutes <= 5));
        await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }
    [Fact]
    public async Task GoalRejectsSkillsFromAnotherSubject()
    {
        using var f = new LearningPathV4Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Proposal("201"));
    }
}
