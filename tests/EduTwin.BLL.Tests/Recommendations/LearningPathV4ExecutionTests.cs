using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathV4ExecutionTests
{
    [Fact]
    public async Task UnfinishedMissionConflict_IdentifiesOwnRun_AndCanResumeWithoutNewRun()
    {
        using var f = new LearningPathV4Fixture();
        var first = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(first.MissionId));
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        var other = agenda.Missions.First(m => m.Purpose == "Study");
        var error = await Assert.ThrowsAsync<EduTwin.BLL.Recommendations.Services.LearningMissionInProgressException>(() =>
            f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(other.MissionId)));
        Assert.Equal(Guid.Parse(first.MissionId), error.MissionId);
        Assert.Equal(run.SessionRunId, error.RunId);
        Assert.Equal(run.Title, error.MissionTitle);
        Assert.Contains(run.Title, error.Message);
        var resumed = await f.Execution.StartMissionAsync(f.Center, f.Student, error.MissionId);
        Assert.Equal(run.SessionRunId, resumed.SessionRunId);
        Assert.Single(await f.Db.LearningPathSessionRuns.ToListAsync());
    }

    [Fact]
    public async Task Review_ReadsSavedOutcomeAndResponses_WithoutEvaluationOrNewRun()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("Study", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, run.Activities[0].ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), FinalAnswer = "Em hiểu cách dùng thì." });
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        var saved = await f.Db.LearningPathSessionRuns.SingleAsync();
        var reviewed = await f.Execution.GetMissionRunAsync(f.Center, f.Student, run.SessionId, run.SessionRunId);
        Assert.Equal(saved.Outcome.ToString(), reviewed.Outcome);
        Assert.Equal("Completed", reviewed.Status);
        Assert.Equal("Em hiểu cách dùng thì.", reviewed.Activities[0].SubmittedResponse);
        Assert.Single(await f.Db.LearningPathSessionRuns.ToListAsync());
        Assert.Empty(await f.Db.Attempts.ToListAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Execution.GetMissionRunAsync(f.Center, Guid.NewGuid(), run.SessionId, run.SessionRunId));
    }
    [Fact]
    public async Task FiveQuestionBank_ReservesFreshItemsForCheck_InsteadOfExposingThemInStudy()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(await f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101 && q.QuestionId > 10105).ToListAsync());
        await f.Db.SaveChangesAsync(); var mission = await f.Activate("Study", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        Assert.Contains("\"example\":null", System.Text.Json.JsonSerializer.Serialize(run.Activities[0].Content!.Payload));
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, run.Activities[0].ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), FinalAnswer = "Read the published introduction" });
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.True(agenda.Missions.Single(m => m.Purpose == "Practice").CanStart);
        var check = agenda.Missions.Single(m => m.Purpose == "IndependentCheck"); Assert.True(check.CanStart);
        Assert.Equal(5, (await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(check.MissionId))).Activities.Count);
    }
    [Fact]
    public async Task Start_PersistsRealIds_IsIdempotent_AndStudyIsNotPractice()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Study", "102");
        var first = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var again = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        Assert.Equal(first.SessionRunId, again.SessionRunId);
        Assert.Single(first.Activities); Assert.Equal("Study", first.Activities[0].ActivityType);
        Assert.Null(first.Activities[0].Question); Assert.Contains("Quá khứ đơn", first.Activities[0].Content!.Title);
        Assert.Equal(1, await f.Db.LearningPathSessionRuns.CountAsync());
    }
    [Fact]
    public async Task Draft_ZeroOrStaleRevisionCannotBypassConflictCheck()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Study");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId)); var a = run.Activities[0];
        var saved = await f.Execution.SaveDraftAsync(f.Center, f.Student, a.ActivityRunId, new SaveActivityDraftRequest { DraftPayload = "my draft", ExpectedRevision = 1 });
        Assert.Equal(2u, saved.DraftRevision);
        var stale = await f.Execution.SaveDraftAsync(f.Center, f.Student, a.ActivityRunId, new SaveActivityDraftRequest { DraftPayload = "overwrite", ExpectedRevision = 0 });
        Assert.True(stale.HasConflict);
        f.Db.ChangeTracker.Clear();
        Assert.Equal("my draft", (await f.Execution.GetActivityRunAsync(f.Center, f.Student, a.ActivityRunId)).DraftPayload);
    }
    [Fact]
    public async Task StudySubmission_DoesNotCreateFakeQuestionAttemptOrMastery()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Study");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var request = new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), FinalAnswer = "Em đã hiểu cách dùng thì hiện tại." };
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, run.Activities[0].ActivityRunId, request);
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, run.Activities[0].ActivityRunId, request);
        Assert.Empty(await f.Db.Attempts.ToListAsync()); Assert.Empty(await f.Db.KnowledgeTwins.ToListAsync());
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal("ActivityOnlyCompleted", result.Outcome); Assert.Empty(result.ProvenSkills);
    }
    [Fact]
    public async Task CrossStudentCannotStartReadDraftOrSubmitAnotherPath()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Study");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId)); var other = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Execution.StartMissionAsync(f.Center, other, Guid.Parse(mission.MissionId)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Execution.GetActivityRunAsync(f.Center, other, run.Activities[0].ActivityRunId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Execution.SaveDraftAsync(f.Center, other, run.Activities[0].ActivityRunId, new SaveActivityDraftRequest()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Execution.SubmitActivityAsync(f.Center, other, run.Activities[0].ActivityRunId, new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid() }));
    }
    [Fact]
    public async Task IndependentCheck_BlocksHelp_RejectsWrongIssuedQuestion_UsesCanonicalSubmitPipeline()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "102");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        Assert.Equal(5, run.Activities.Count);
        var a = run.Activities[0];
        var help = await f.Execution.RequestAssistanceAsync(f.Center, f.Student, a.ActivityRunId, new RequestAssistanceRequest()); Assert.True(help.IsBlocked);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = 20101, FinalAnswer = "45" }));
        var request = new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, FinalAnswer = "wrong-45" };
        var result = await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, request);
        Assert.NotNull(result.AttemptId);
        var attempt = await f.Db.Attempts.SingleAsync(); Assert.False(attempt.IsCorrect); Assert.False(attempt.IsPostFeedback);
        Assert.Single(await f.Db.AIAnalysisJobs.ToListAsync());
        Assert.Equal(result.AttemptId, (await f.Db.IssuedItems.SingleAsync(i => i.ActivityRunId == a.ActivityRunId)).AttemptId);
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, request);
        Assert.Single(await f.Db.Attempts.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = request.ClientSubmissionId, QuestionId = request.QuestionId, FinalAnswer = "changed" }));
    }
    [Fact]
    public async Task MissingNewQuestions_ExplainsShortage_InsteadOfReusingExposedQuestions()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "101");
        for (ulong i = 1; i <= 9; i++) await f.RecordEvidence(101, i);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId)));
        Assert.Contains("3/5", error.Message); Assert.Empty(await f.Db.LearningPathSessionRuns.ToListAsync());
    }
    [Fact]
    public async Task CannotSkipAheadToSubmitLaterActivity()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Practice", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var a = run.Activities[1];
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, FinalAnswer = "answer-2" }));
        Assert.Empty(await f.Db.Attempts.ToListAsync());
    }
}
