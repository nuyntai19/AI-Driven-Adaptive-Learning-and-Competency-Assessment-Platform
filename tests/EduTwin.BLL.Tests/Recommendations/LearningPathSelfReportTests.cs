using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathSelfReportTests
{
    private static void Edge(LearningPathV4Fixture f, ulong source, ulong target) => f.AddPrerequisite(source, target);

    [Theory]
    [InlineData("Placement")]
    [InlineData("Foundation")]
    public async Task SelfReport_SkipsOnlyChosenFoundations_WithoutInventingMastery_AndSurvivesReload(string goalType)
    {
        using var f = new LearningPathV4Fixture();
        Edge(f, 101, 102); Edge(f, 102, 103); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest {
            SubjectId = f.English, GoalType = goalType, TargetSkillIds = ["103"], SelfReportedPrerequisiteSkillIds = ["101", "102"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        Assert.Equal("103", Assert.Single(proposal.Milestones).SkillIds.Single());
        Assert.Equal(new[] { "101", "102" }, proposal.SelfReportedPrerequisiteSkillIds);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        f.Db.ChangeTracker.Clear();
        var restored = (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!;
        Assert.Equal(new[] { "103" }, restored.TargetSkillIds);
        Assert.Equal(new[] { "101", "102" }, restored.SelfReportedPrerequisiteSkillIds);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(new[] { "101", "102" }, agenda.SelfReportedPrerequisiteSkillIds);
        Assert.All(agenda.Missions, m => Assert.Equal("103", m.SkillId));
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.All(map.Skills, s => Assert.Equal("Unknown", s.State));
        var check = agenda.Missions.Single(m => m.Purpose == "IndependentCheck");
        Assert.True(check.CanStart);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId));
        Assert.Equal(5, run.Activities.Count);
    }

    [Fact]
    public async Task Customization_PreservesSelfReports_WithoutReaddingSkippedFoundations()
    {
        using var f = new LearningPathV4Fixture();
        Edge(f, 101, 103); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest {
            SubjectId = f.English, TargetSkillIds = ["103"], SelfReportedPrerequisiteSkillIds = ["101"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        var customized = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, proposal.ProposalId,
            new CustomizePathProposalRequest { TargetSkillIds = ["103"], AvailableMinutesPerDay = 20 });
        Assert.Equal("103", Assert.Single(customized.Milestones).SkillIds.Single());
        Assert.Equal(new[] { "101" }, customized.SelfReportedPrerequisiteSkillIds);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, customized.ProposalId);
        Assert.Equal(new[] { "101" }, (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!.SelfReportedPrerequisiteSkillIds);
    }

    [Fact]
    public async Task OneSelfReportedPrerequisite_DoesNotUnlockAnotherUnconfirmedPrerequisite()
    {
        using var f = new LearningPathV4Fixture();
        Edge(f, 101, 103); Edge(f, 102, 103); await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest {
            SubjectId = f.English, TargetSkillIds = ["103"], SelfReportedPrerequisiteSkillIds = ["101"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        Assert.DoesNotContain(proposal.Milestones, m => m.SkillIds.Contains("101"));
        Assert.Contains(proposal.Milestones, m => m.SkillIds.Contains("102"));
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var check = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.SkillId == "103" && m.Purpose == "IndependentCheck");
        Assert.False(check.CanStart);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId)));
    }

    [Theory]
    [InlineData("103", "101")]
    [InlineData("103", "201")]
    [InlineData("101", "101")]
    public async Task SelfReports_RejectUnrelatedOutOfSubjectOrSelectedSkills(string target, string claimed)
    {
        using var f = new LearningPathV4Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student,
            new CreateLearningGoalRequest { SubjectId = f.English, TargetSkillIds = [target], SelfReportedPrerequisiteSkillIds = [claimed] }));
    }
}
