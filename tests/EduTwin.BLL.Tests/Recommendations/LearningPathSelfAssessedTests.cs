using System.Text.Json;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

/// <summary>
/// Parts left out of the placement check are "self-assessed as mastered": never counted as mastered,
/// and the route ends with one short confirmation review per part.
/// </summary>
public sealed class LearningPathSelfAssessedTests
{
    private static async Task<PathProposalDto> Proposal(LearningPathV4Fixture f, string[] targets, string[] selfAssessed)
    {
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest {
            SubjectId = f.English, GoalType = "KeepUp", TargetSkillIds = [.. targets], SelfAssessedSkillIds = [.. selfAssessed] });
        return await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
    }

    private static async Task<PathProposalDto> SavedPlan(LearningPathV4Fixture f, Guid pathId)
    {
        var path = await f.Db.LearningPaths.AsNoTracking().SingleAsync(p => p.LearningPathId == pathId);
        return JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public async Task UnselectedParts_GetOneShortConfirmationReview_AtTheEnd()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Proposal(f, ["103"], ["101", "102"]);
        Assert.Equal(new[] { "103", "101", "102" }, proposal.Milestones.Select(m => m.SkillIds.Single()));
        Assert.False(proposal.Milestones[0].IsSelfAssessedCheck);
        foreach (var milestone in proposal.Milestones.Skip(1))
        {
            Assert.True(milestone.IsSelfAssessedCheck);
            var review = Assert.Single(milestone.Missions);
            Assert.Equal("Review", review.Purpose);
            Assert.True(review.IsSelfAssessedCheck);
            Assert.Equal(LearningPathPacing.ConfirmationSize, review.QuestionCount);
            Assert.Null(review.AvailableFrom);
        }
        Assert.Equal(new[] { "101", "102" }, proposal.SelfAssessedSkillIds);
    }

    [Fact]
    public async Task SelfAssessedParts_AreNotMastered_AndWaitForTheMainWork()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Proposal(f, ["103"], ["101"]);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(new[] { "101" }, (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!.SelfAssessedSkillIds);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 300);
        Assert.Equal(new[] { "101" }, agenda.SelfAssessedSkillIds);
        var review = agenda.Missions.Single(m => m.SkillId == "101");
        Assert.True(review.IsSelfAssessedCheck);
        Assert.True(review.CanStart);
        Assert.Null(review.WeaknessLevel);
        Assert.DoesNotContain(agenda.ReviewDueTasks, m => m.MissionId == review.MissionId);
        Assert.DoesNotContain(agenda.TodayPlan, m => m.MissionId == review.MissionId);
        Assert.NotNull(review.Advice);
        Assert.Equal(1u, agenda.Progress!.TotalSkills);
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.All(map.Skills, s => Assert.NotEqual("Confirmed", s.State));
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(review.MissionId));
        Assert.Equal((int)LearningPathPacing.ConfirmationSize, run.Activities.Count);
    }

    [Fact]
    public async Task SelfAssessedPrerequisite_IsNotPulledIntoLearning_AndDoesNotBlockTheCheck()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.KnowledgeEdges.Add(new DAL.KnowledgeGraph.KnowledgeEdge { CenterId = f.Center, SubjectId = f.English, SourceNodeId = 101, TargetNodeId = 103,
            RelationType = Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var proposal = await Proposal(f, ["103"], ["101"]);
        Assert.Single(proposal.Milestones, m => m.SkillIds.Contains("101"));
        Assert.True(proposal.Milestones.Single(m => m.SkillIds.Contains("101")).IsSelfAssessedCheck);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.Missions.Single(m => m.SkillId == "103" && m.Purpose == "IndependentCheck").CanStart);
    }

    [Fact]
    public async Task FailedConfirmation_BecomesLearning_WithRepairSetAndSelfCheck()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Proposal(f, ["103"], ["101"]);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var review = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.SkillId == "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(review.MissionId));
        var saved = await f.Db.LearningPathSessionRuns.Include(r => r.Session).SingleAsync(r => r.SessionRunId == run.SessionRunId);
        await LearningPathFollowUp.AddAsync(f.Db, saved, "ActivityOnlyCompleted", DateTime.UtcNow, default, .33m);
        await f.Db.SaveChangesAsync();
        var milestone = (await SavedPlan(f, saved.Session.LearningPathId)).Milestones.Single(m => m.SkillIds.Contains("101"));
        Assert.False(milestone.IsSelfAssessedCheck);
        Assert.Contains(milestone.Missions, m => m.Purpose == "Practice" && m.IsRepair);
        Assert.Contains(milestone.Missions, m => m.Purpose == "IndependentCheck");
        Assert.Equal(2, await f.Db.LearningPathSessions.CountAsync(s => s.LearningPathId == saved.Session.LearningPathId && s.TopicNodeId == 101 &&
            (s.Objective == "Practice" || s.Objective == "IndependentCheck")));
    }

    [Fact]
    public async Task PassedConfirmation_StaysSelfAssessed_WithSpacedReview()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Proposal(f, ["103"], ["101"]);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var review = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.SkillId == "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(review.MissionId));
        var saved = await f.Db.LearningPathSessionRuns.Include(r => r.Session).SingleAsync(r => r.SessionRunId == run.SessionRunId);
        await LearningPathFollowUp.AddAsync(f.Db, saved, "ActivityOnlyCompleted", DateTime.UtcNow, default, 1m);
        await f.Db.SaveChangesAsync();
        var milestone = (await SavedPlan(f, saved.Session.LearningPathId)).Milestones.Single(m => m.SkillIds.Contains("101"));
        Assert.True(milestone.IsSelfAssessedCheck);
        Assert.DoesNotContain(milestone.Missions, m => m.Purpose == "Practice");
        Assert.Contains(milestone.Missions, m => m.Purpose == "Review" && m.AvailableFrom > DateTime.UtcNow);
    }

    [Fact]
    public async Task Customization_KeepsConfirmationReviews_AndTheirScope()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Proposal(f, ["102", "103"], ["101"]);
        var customized = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, proposal.ProposalId,
            new CustomizePathProposalRequest { TargetSkillIds = ["103", "101"], AvailableMinutesPerDay = 20 });
        Assert.Equal(new[] { "103", "101" }, customized.Milestones.Select(m => m.SkillIds.Single()));
        Assert.True(customized.Milestones.Last().IsSelfAssessedCheck);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, customized.ProposalId);
        var goal = (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!;
        Assert.Equal(new[] { "103" }, goal.TargetSkillIds);
        Assert.Equal(new[] { "101" }, goal.SelfAssessedSkillIds);
    }

    [Theory]
    [InlineData("103", "103")]
    [InlineData("103", "201")]
    public async Task SelfAssessed_RejectsSelectedOrOutOfSubjectSkills(string target, string claimed)
    {
        using var f = new LearningPathV4Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student,
            new CreateLearningGoalRequest { SubjectId = f.English, TargetSkillIds = [target], SelfAssessedSkillIds = [claimed] }));
    }

    [Fact]
    public async Task Placement_IgnoresSelfAssessedParts()
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest {
            SubjectId = f.English, GoalType = "Placement", TargetSkillIds = ["103"], SelfAssessedSkillIds = ["101"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        Assert.Empty(proposal.SelfAssessedSkillIds);
        Assert.DoesNotContain(proposal.Milestones, m => m.IsSelfAssessedCheck);
    }

    [Fact]
    public async Task Placement_ReusesConfirmedPrerequisite_ButTestsTheChosenPart()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.KnowledgeEdges.Add(new DAL.KnowledgeGraph.KnowledgeEdge { CenterId = f.Center, SubjectId = f.English, SourceNodeId = 101, TargetNodeId = 103,
            RelationType = Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i);
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest {
            SubjectId = f.English, GoalType = "Placement", TargetSkillIds = ["103"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        Assert.Empty(proposal.Milestones.Single(m => m.SkillIds.Contains("101")).Missions);
        Assert.Single(proposal.Milestones.Single(m => m.SkillIds.Contains("103")).Missions);
    }

    [Fact]
    public async Task ExtendCheck_AddsTestsToTheActiveRoute_WithoutReplacingIt()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.KnowledgeEdges.Add(new DAL.KnowledgeGraph.KnowledgeEdge { CenterId = f.Center, SubjectId = f.English, SourceNodeId = 101, TargetNodeId = 102,
            RelationType = Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        var proposal = await Proposal(f, ["103"], ["101"]);
        var accepted = await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var result = await f.Planner.ExtendPathWithCheckAsync(f.Center, f.Student, new ExtendPathCheckRequest { SubjectId = f.English, SkillIds = ["101", "102"] });
        Assert.Equal(2, result.MissionIds.Count);
        Assert.Empty(result.UnavailableSkillIds);
        f.Db.ChangeTracker.Clear();
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(accepted.LearningPathId, agenda.LearningPathId); // Same route: nothing was replaced.
        Assert.Contains(agenda.Missions, m => m.SkillId == "103" && m.Purpose == "Practice");
        Assert.Empty(agenda.SelfAssessedSkillIds);
        Assert.Equal("Skipped", agenda.Missions.Single(m => m.SkillId == "101" && m.IsSelfAssessedCheck).Status);
        foreach (var id in result.MissionIds)
            Assert.True(agenda.Missions.Single(m => m.MissionId == id).CanStart);
        var goal = (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!;
        Assert.Equal(new[] { "103", "101", "102" }, goal.TargetSkillIds);
        Assert.Empty(goal.SelfAssessedSkillIds);
        // 102 needs 101, which is not confirmed yet: an extra check still starts because it measures, not certifies a sequence.
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(result.MissionIds[1]));
        Assert.Equal(5, run.Activities.Count);
    }

    [Fact]
    public async Task Agenda_ReportsCapacity_SoAnyLargerTimeBudgetAddsNothing()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Proposal(f, ["103"], []);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var small = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 10);
        var large = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 300);
        Assert.Equal(large.PlannedMinutes, small.AvailableTodayMinutes);
        Assert.True(small.PlannedMinutes <= small.AvailableTodayMinutes);
        Assert.False(string.IsNullOrWhiteSpace(small.PrimaryReason));
    }
}
