using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class GradeScopedLearningV4Tests
{
    [Fact]
    public async Task MissingExternalEvidence_UsesFreshTargetAssessment_AndDoesNotAssertExternalMastery()
    {
        using var f = new LearningPathV4Fixture();
        await AddExternalPrerequisite(f);
        var proposal = await f.Proposal("102");
        var check = proposal.Milestones.SelectMany(m => m.Missions).Single(m => m.Purpose == "IndependentCheck");
        var practice = proposal.Milestones.SelectMany(m => m.Missions).Single(m => m.Purpose == "Practice");
        Assert.True(check.IsExternalPrerequisiteAssessment);
        Assert.True(check.CanStart);
        Assert.False(practice.CanStart);
        Assert.Contains("không xác nhận năng lực của nền tảng", check.SelectionReason);
        Assert.Equal(check.MissionId, proposal.InitialMission!.MissionId);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(practice.MissionId)));
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId));
        Assert.Equal(5, run.Activities.Count);
        foreach (var activity in run.Activities)
        {
            var qid = activity.Question!.Question!.QuestionId;
            var question = await f.Db.Questions.SingleAsync(q => q.QuestionId == qid);
            Assert.Equal(102ul, question.PrimaryTopicNodeId); Assert.Equal((byte)12, question.GradeLevel);
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, activity.ActivityRunId,
                new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = $"answer-{qid % 100}" });
        }
        foreach (var attempt in await f.Db.Attempts.ToListAsync())
        { attempt.Status = AttemptStatus.Completed; await f.Assess(attempt); }
        foreach (var job in await f.Db.AIAnalysisJobs.ToListAsync()) job.Status = AIJobStatus.Completed;
        await f.Db.SaveChangesAsync();
        Assert.Equal("Passed", (await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId)).Outcome);
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English, null);
        Assert.Equal("Confirmed", map.Skills.Single(s => s.SkillId == "102").State);
        Assert.Equal("Available", map.Skills.Single(s => s.SkillId == "102").Availability);
        Assert.Equal(0m, map.ExternalPrerequisiteMastery["104"]);
        Assert.DoesNotContain(map.Skills, s => s.SkillId == "104");
        Assert.Empty(await f.Db.KnowledgeTwins.Where(t => t.TopicNodeId == 104).ToListAsync());
        Assert.Empty(await f.Db.Attempts.Where(a => a.Question.PrimaryTopicNodeId == 104).ToListAsync());
    }

    [Fact]
    public async Task ExternalAssessment_DoesNotBypassUnmetInternalPrerequisite()
    {
        using var f = new LearningPathV4Fixture();
        await AddExternalPrerequisite(f, withInternalPrerequisite: true);
        var proposal = await f.Proposal("102");
        var check = proposal.Milestones.Single(m => m.SkillIds.Contains("102")).Missions.Single(m => m.Purpose == "IndependentCheck");
        Assert.True(check.IsExternalPrerequisiteAssessment); Assert.False(check.CanStart);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId)));
        Assert.Empty(await f.Db.LearningPathSessionRuns.ToListAsync());
    }

    [Fact]
    public async Task OldV4Policy_RequiresNewProposal_AndIsStillRecognizedAsV4()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("IndependentCheck", "102");
        var session = await f.Db.LearningPathSessions.SingleAsync(s => s.SessionId == Guid.Parse(mission.MissionId));
        session.PolicyVersion = "v4-evidence-1"; await f.Db.SaveChangesAsync();
        Assert.True(LearningPathV4Data.IsV4Policy(session.PolicyVersion));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, session.SessionId));
        Assert.Contains("Lộ trình cũ", error.Message);
    }

    private static async Task AddExternalPrerequisite(LearningPathV4Fixture f, bool withInternalPrerequisite = false)
    {
        (await f.Db.Students.SingleAsync()).GradeLevel = 12;
        var target = await f.Db.KnowledgeGraphVersions.SingleAsync(v => v.SubjectId == f.English);
        target.GradeLevel = 12;
        foreach (var question in await f.Db.Questions.Where(q => q.SubjectId == f.English).ToListAsync()) question.GradeLevel = 12;
        f.Db.KnowledgeNodes.Add(new KnowledgeNode { CenterId = f.Center, SubjectId = f.English, NodeId = 104,
            NodeCode = "EXTERNAL-BASE", NodeName = "Nền tảng khối 11", NodeType = NodeType.Skill, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var lower = GraphScopeFixture.Approve(f.Db, f.Center, f.English, 11, 104);
        f.Db.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = f.Center, SubjectId = f.English, EdgeId = 50,
            SourceNodeId = 104, TargetNodeId = 102, RelationType = RelationType.PrerequisiteOf, Weight = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        f.Db.KnowledgeEdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = f.Center, GraphVersionId = target.GraphVersionId,
            EdgeId = 50, SourceGraphVersionId = lower, RelationType = RelationType.PrerequisiteOf, Weight = 1 });
        if (withInternalPrerequisite)
        {
            f.Db.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = f.Center, SubjectId = f.English, EdgeId = 51,
                SourceNodeId = 103, TargetNodeId = 102, RelationType = RelationType.PrerequisiteOf, Weight = 1,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            f.Db.KnowledgeEdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = f.Center, GraphVersionId = target.GraphVersionId,
                EdgeId = 51, RelationType = RelationType.PrerequisiteOf, Weight = 1 });
        }
        await f.Db.SaveChangesAsync();
    }
}
