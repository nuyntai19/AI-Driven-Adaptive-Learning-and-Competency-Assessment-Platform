using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathV4EvaluatorTests
{
    [Fact]
    public async Task UnsubmittedActivities_CannotBeMarkedPassed()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("InsufficientEvidence", result.Outcome);
        Assert.Equal(SessionRunStatus.InProgress, (await f.Db.LearningPathSessionRuns.SingleAsync()).Status);
    }
    [Theory]
    [InlineData(5, false, "Passed")]
    [InlineData(4, false, "Passed")]
    [InlineData(1, false, "NeedsSupport")]
    [InlineData(5, true, "Passed")]
    public async Task RealSubmissions_AreEvaluatedAgainstEvidenceGate_NotActivityScores(int correct, bool needsReview, string outcome)
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        for (var i = 0; i < run.Activities.Count; i++)
        {
            var a = run.Activities[i]; var qid = a.Question!.Question!.QuestionId;
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new SubmitActivityRequest
            { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = i < correct ? $"answer-{qid % 100}" : "wrong-45" });
        }
        var pending = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("PendingGrading", pending.Outcome);
        // Simulate only the asynchronous worker boundary: gate decisions are real, no fake Activity score is used.
        foreach (var a in await f.Db.Attempts.ToListAsync()) { a.Status = AttemptStatus.Completed; await f.Assess(a, needsReview); }
        foreach (var j in await f.Db.AIAnalysisJobs.ToListAsync()) j.Status = AIJobStatus.Completed;
        await f.Db.SaveChangesAsync();
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal(outcome, result.Outcome);
        var decisions = await f.Db.PathDecisions.CountAsync();
        var repeated = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal(outcome, repeated.Outcome); Assert.Equal(decisions, await f.Db.PathDecisions.CountAsync());
        Assert.Equal(5, await f.Db.Attempts.CountAsync());
        Assert.Empty(await f.Db.TwinUpdateHistories.ToListAsync()); // Evaluator never fabricates Twin changes.
    }
    [Fact]
    public async Task CorrectAnswersWithoutEvidence_CannotConfirmAnything()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        foreach (var a in run.Activities) await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, FinalAnswer = "answer-1" });
        foreach (var a in await f.Db.Attempts.ToListAsync()) a.Status = AttemptStatus.Completed;
        foreach (var job in await f.Db.AIAnalysisJobs.ToListAsync()) job.Status = AIJobStatus.Completed;
        await f.Db.SaveChangesAsync();
        Assert.Equal("InsufficientEvidence", (await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId)).Outcome);
    }
}
