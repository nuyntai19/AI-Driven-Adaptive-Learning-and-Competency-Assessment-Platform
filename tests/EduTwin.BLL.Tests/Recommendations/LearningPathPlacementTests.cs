using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathPlacementTests
{
    private static async Task<PathProposalDto> Placement(LearningPathV4Fixture f, params string[] ids)
    {
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest
        { SubjectId = f.English, GoalType = "Placement", Title = "Kiểm tra đầu vào", TargetSkillIds = ids.ToList() });
        return await f.Planner.GeneratePathProposalAsync(f.Center, f.Student,
            new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
    }

    private static async Task<MissionRunDto> Complete(LearningPathV4Fixture f, ProposedMissionDto mission, int correct, bool review = false)
    {
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        for (var i = 0; i < run.Activities.Count; i++)
        {
            var activity = run.Activities[i];
            var qid = activity.Question!.Question!.QuestionId;
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, activity.ActivityRunId, new SubmitActivityRequest
            { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = i < correct ? $"answer-{qid % 100}" : "wrong" });
        }
        foreach (var attempt in await f.Db.Attempts.Where(a => a.SessionRunId == run.SessionRunId).ToListAsync())
        { attempt.Status = AttemptStatus.Completed; await f.Assess(attempt, review); }
        foreach (var job in await f.Db.AIAnalysisJobs.ToListAsync()) job.Status = AIJobStatus.Completed;
        await f.Db.SaveChangesAsync();
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        return run;
    }

    [Fact]
    public async Task TopicPrerequisites_ExpandToLeaves_AndPlacementCanAssessWithoutPriorMastery()
    {
        using var f = new LearningPathV4Fixture();
        f.AddPrerequisite(100, 103);
        await f.Db.SaveChangesAsync();
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.Equal("Thì", map.Skills.Single(s => s.SkillId == "101").TopicName);
        Assert.Equal("100", map.Skills.Single(s => s.SkillId == "101").TopicId);
        Assert.Equal(new[] { "101", "102" }, map.Skills.Single(s => s.SkillId == "103").PrerequisiteSkillIds.Order());
        var proposal = await Placement(f, "103");
        Assert.True(proposal.IsPlacement);
        Assert.Equal(new[] { "103" }, proposal.RequestedSkillIds);
        Assert.Equal(new[] { "101", "102", "103" }, proposal.Milestones.SelectMany(m => m.SkillIds));
        Assert.All(proposal.Milestones, m => Assert.Equal("IndependentCheck", Assert.Single(m.Missions).Purpose));
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var dependent = proposal.Milestones.Last().Missions.Single();
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.Missions.Single(m => m.MissionId == dependent.MissionId).CanStart);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(dependent.MissionId));
        Assert.Equal(5, run.Activities.Count);
        var assistance = await f.Execution.RequestAssistanceAsync(f.Center, f.Student,
            run.Activities[0].ActivityRunId, new RequestAssistanceRequest());
        Assert.True(assistance.IsBlocked);
        Assert.Null(run.Activities[0].LearningFeedback);
    }

    [Fact]
    public async Task FailedPlacement_ProducesScopedScore_NoRepairLoop_AndCanTransitionWithHistory()
    {
        using var f = new LearningPathV4Fixture();
        await f.RecordEvidence(201, 1); // Another subject must never influence the report.
        var proposal = await Placement(f, "101", "102");
        var accepted = await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        await Complete(f, proposal.Milestones[0].Missions.Single(), 2);
        var partial = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.False(partial.PlacementReport!.IsComplete);
        Assert.Equal(4m, partial.PlacementReport.ScoreOutOfTen);
        Assert.Equal("NotStarted", partial.PlacementReport.Skills.Single(s => s.SkillId == "102").Status);
        Assert.DoesNotContain(partial.Missions, m => m.Purpose == "Practice" || m.Purpose == "Review");
        await Complete(f, proposal.Milestones[1].Missions.Single(), 5);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.PlacementReport!.IsComplete);
        Assert.Equal(7m, agenda.PlacementReport.ScoreOutOfTen);
        Assert.Equal(4u, agenda.PlacementReport.Skills.Single(s => s.SkillId == "101").EstimatedPracticeSets);
        Assert.Equal(0u, agenda.PlacementReport.Skills.Single(s => s.SkillId == "102").EstimatedPracticeSets);
        Assert.All(agenda.Missions, m => Assert.Equal("Completed", m.Status));
        Assert.Null(agenda.PrimaryMission);
        Assert.False(agenda.CanAdvance); // Completing the baseline is not completing the learning goal.
        Assert.Equal(10, await f.Db.Attempts.CountAsync(a => a.Question.SubjectId == f.English));
        var learning = await f.Proposal(agenda.TargetSkillIds.ToArray());
        Assert.False(learning.IsPlacement);
        var weak = learning.Milestones.Single(m => m.SkillIds.Contains("101"));
        Assert.Equal(4u, weak.Missions.Single(m => m.Purpose == "Practice").EstimatedSets);
        Assert.DoesNotContain(learning.Milestones.Single(m => m.SkillIds.Contains("102")).Missions, m => m.Purpose == "Practice");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, learning.ProposalId);
        var updated = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Contains(updated.PathHistory, h => h.LearningPathId == accepted.LearningPathId && h.AnsweredQuestions == 10);
        Assert.Equal(10u, updated.Insights.AnswerCount);
        var oldResult = await f.Execution.GetMissionRunAsync(f.Center, f.Student,
            Guid.Parse(proposal.Milestones[0].Missions.Single().MissionId), (await f.Db.LearningPathSessionRuns.OrderBy(r => r.SessionRunId).FirstAsync()).SessionRunId);
        Assert.Equal("Completed", oldResult.Status);
    }

    [Fact]
    public async Task ShortQuestionBank_DoesNotScoreUntestedSkillAsZero_OrLockTheOtherSections()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(await f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101 && q.QuestionId > 10103).ToListAsync());
        await f.Db.SaveChangesAsync();
        var proposal = await Placement(f, "101", "102");
        Assert.False(proposal.Milestones.First().Missions.Single().CanStart);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        await Complete(f, proposal.Milestones.Last().Missions.Single(), 5);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        var missing = agenda.PlacementReport!.Skills.Single(s => s.SkillId == "101");
        Assert.Equal("Unavailable", missing.Status);
        Assert.Null(missing.ScoreOutOfTen);
        Assert.Equal(10m, agenda.PlacementReport.ScoreOutOfTen);
        Assert.False(agenda.PlacementReport.IsComplete);
    }

    [Fact]
    public async Task ChosenConfirmedSkill_IsStillTested_WithoutFabricatedScoreBeforeTheTest()
    {
        using var f = new LearningPathV4Fixture();
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i);
        var proposal = await Placement(f, "101");
        // The student explicitly chose this part: a placement must give a test, not jump to a route.
        Assert.Equal("IndependentCheck", Assert.Single(proposal.Milestones.Single().Missions).Purpose);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal("NotStarted", agenda.PlacementReport!.Skills.Single().Status);
        Assert.False(agenda.PlacementReport.IsComplete);
        Assert.Null(agenda.PlacementReport.ScoreOutOfTen); // Historical evidence is not a fabricated new test score.
        Assert.True(agenda.Missions.Single().CanStart);
        var learning = await f.Proposal("101");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, learning.ProposalId);
        var next = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(next.CanAdvance);
        Assert.Contains("102", next.NextSkillIds);
    }

    [Fact]
    public async Task ChosenConfirmedSkill_WithoutFreshQuestions_ReusesEvidence()
    {
        using var f = new LearningPathV4Fixture();
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i, false);
        for (ulong i = 6; i <= 10; i++) await f.RecordEvidence(101, i);
        var proposal = await Placement(f, "101");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.False(agenda.Missions.Single().CanStart); // 2 of 12 questions left: not enough for a fair new test.
        Assert.Equal("Confirmed", agenda.PlacementReport!.Skills.Single().Status);
        Assert.True(agenda.PlacementReport.IsComplete);
        Assert.Null(agenda.PlacementReport.ScoreOutOfTen);
    }

    [Fact]
    public async Task ProvisionalPass_DoesNotClaimConfirmedMasteryOrUnlockNextStage()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Placement(f, "101");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        await Complete(f, proposal.Milestones.Single().Missions.Single(), 5, review: true);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.PlacementReport!.IsComplete);
        Assert.True(agenda.PlacementReport.Skills.Single().EstimatedPracticeSets > 0);
        Assert.False(agenda.CanAdvance);
        Assert.NotEqual("Confirmed", (await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English)).Skills.Single(s => s.SkillId == "101").State);
    }

    [Fact]
    public async Task ReportUsesEarnedPoints_IncludingPartialCredit()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await Placement(f, "101");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var run = await Complete(f, proposal.Milestones.Single().Missions.Single(), 2);
        var partialCredit = await f.Db.Attempts.FirstAsync(a => a.SessionRunId == run.SessionRunId && a.IsCorrect == false);
        partialCredit.AwardedScore = 5;
        await f.Db.SaveChangesAsync();
        var report = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).PlacementReport!;
        Assert.Equal(5m, report.ScoreOutOfTen);
        Assert.Equal(2u, report.Skills.Single().CorrectAnswers);
    }

    [Fact]
    public async Task MultipleChoiceMastery_UsesTheSameStricterThresholdAsIndependentChecks()
    {
        using var f = new LearningPathV4Fixture();
        foreach (var question in await f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync())
            question.QuestionType = EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice;
        await f.Db.SaveChangesAsync();
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i, i != 5);
        var map = await f.Planner.GetCapabilityMapAsync(f.Center, f.Student, f.English);
        Assert.Equal("Developing", map.Skills.Single(s => s.SkillId == "101").State);
    }

    [Fact]
    public async Task AdvancedGoal_CannotAdvanceJustBecauseItsBasicSkillsWereAlreadyConfirmed()
    {
        using var f = new LearningPathV4Fixture();
        for (ulong i = 1; i <= 5; i++) await f.RecordEvidence(101, i);
        foreach (var question in await f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync()) question.Difficulty = 4;
        await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest
        { SubjectId = f.English, GoalType = "Advanced", TargetSkillIds = ["101"] });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student,
            new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.False(agenda.CanAdvance);
        Assert.NotNull(agenda.PrimaryMission);
    }
}
