using EduTwin.Contracts.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathConnectedExperienceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdvancedRoute_UsesActualHardQuestions_OrExplainsTheirAbsence(bool hasHardQuestions)
    {
        using var f = new LearningPathV4Fixture();
        for (ulong i=1;i<=5;i++) await f.RecordEvidence(101, i);
        if (hasHardQuestions) foreach(var q in await f.Db.Questions.Where(q=>q.PrimaryTopicNodeId==101 && q.QuestionId>=10109).ToListAsync()) q.Difficulty=4;
        await f.Db.SaveChangesAsync();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest { SubjectId=f.English, Title="Nâng cao", GoalType="Advanced", TargetSkillIds=["101"] });
        var p = await f.Planner.GeneratePathProposalAsync(f.Center,f.Student,new GeneratePathProposalRequest { SubjectId=f.English,GoalId=goal.GoalId });
        var m=Assert.Single(p.Milestones[0].Missions);Assert.Equal("Practice",m.Purpose); Assert.Equal((byte)4,m.DifficultyFloor);Assert.Equal(hasHardQuestions,m.CanStart);
        await f.Planner.AcceptPathProposalAsync(f.Center,f.Student,p.ProposalId);
        if(hasHardQuestions){
            var agenda=await f.Planner.GetLearningAgendaAsync(f.Center,f.Student,f.English);Assert.NotNull(agenda.PrimaryMission);
            var run=await f.Execution.StartMissionAsync(f.Center,f.Student,Guid.Parse(m.MissionId));Assert.Equal(4,run.Activities.Count);
            Assert.All(run.Activities,a=>Assert.True(a.Question!.Question!.QuestionId>=10109));
        } else {
            Assert.Contains("Chưa có câu khó",m.SelectionReason);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Execution.StartMissionAsync(f.Center,f.Student,Guid.Parse(m.MissionId)));
        }
    }
    [Fact]
    public async Task CancelledUnstartedMission_CannotBeStartedByCallingItsOldUrl()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Practice", "101");
        var session = await f.Db.LearningPathSessions.SingleAsync(s => s.SessionId == Guid.Parse(mission.MissionId));
        session.Status = LearningPathSessionStatus.Skipped; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, session.SessionId));
        Assert.Empty(await f.Db.LearningPathSessionRuns.ToListAsync());
    }
    [Fact]
    public async Task RepairPractice_ReusesAvailableMisses_AndPreservesFiveFreshCheckQuestions()
    {
        using var f = new LearningPathV4Fixture();
        for (ulong i=1; i<=7; i++) await f.RecordEvidence(101, i, false);
        var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        Assert.All(run.Activities, a => Assert.InRange(a.Question!.Question!.QuestionId, 10101ul, 10107ul));
    }
    [Fact]
    public async Task Insights_CountDistinctLatestAnswers_RecommendActualWeakness_ExcludeOtherSubject()
    {
        using var f = new LearningPathV4Fixture();
        await f.RecordEvidence(101, 1, false); await f.RecordEvidence(101, 1, true);
        await f.RecordEvidence(102, 2, false); await f.RecordEvidence(201, 1, false);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(3u, agenda.Insights.AnswerCount); Assert.Equal(2u, agenda.Insights.GradedQuestionCount);
        Assert.Equal(new[] { "102" }, agenda.Insights.SuggestedSkillIds);
        Assert.Equal(0u, agenda.Insights.Skills.Single(s => s.SkillId == "101").WrongCount);
        Assert.All(agenda.Insights.Skills, s => Assert.Empty(s.ConfirmedMisconceptions));
        Assert.Null(agenda.LearningPathId); // Insight works before creating a path.
        var p = await f.Proposal(); Assert.Equal("102", p.Milestones[0].SkillIds[0]);
    }

    [Fact]
    public async Task Practice_PersistsASequence_ShowsImmediateAuthoredFeedback_AndRetryRestoresIt()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        Assert.InRange(run.Activities.Count, 5, 8);
        Assert.Equal(run.Activities.Count, run.Activities.Select(a => a.Question!.Question!.QuestionId).Distinct().Count());
        Assert.All(run.Activities, a => Assert.Null(a.LearningFeedback));
        var first = run.Activities[0]; var qid = first.Question!.Question!.QuestionId;
        var body = new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = "incorrect" };
        var response = await f.Execution.SubmitActivityAsync(f.Center, f.Student, first.ActivityRunId, body);
        Assert.False(response.LearningFeedback!.IsCorrect); Assert.Equal("Preliminary", response.LearningFeedback.Status);
        Assert.Contains("Giải thích đã xuất bản", response.LearningFeedback.Explanation);
        Assert.Equal($"answer-{qid % 100}", response.LearningFeedback.CorrectAnswer);
        var replay = await f.Execution.SubmitActivityAsync(f.Center, f.Student, first.ActivityRunId, body);
        Assert.Equal(response.AttemptId, replay.AttemptId); Assert.Equal(response.LearningFeedback.Explanation, replay.LearningFeedback!.Explanation);
        var restored = await f.Execution.GetActivityRunAsync(f.Center, f.Student, first.ActivityRunId);
        Assert.Equal(response.LearningFeedback.CorrectAnswer, restored.LearningFeedback!.CorrectAnswer);
        Assert.True((await f.Db.Attempts.SingleAsync()).IsPostFeedback);
    }

    [Fact]
    public async Task IndependentFeedback_IsWithheldUntilEveryQuestionIsSubmitted()
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        foreach (var a in run.Activities)
        {
            var response = await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId,
                new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, FinalAnswer = "wrong" });
            if (a != run.Activities.Last()) Assert.Null(response.LearningFeedback);
        }
        Assert.NotNull((await f.Execution.GetActivityRunAsync(f.Center, f.Student, run.Activities[0].ActivityRunId)).LearningFeedback);
    }

    [Fact]
    public async Task SmallBank_AllowsFivePracticeQuestions_ThenReportsNoFreshIndependentEvidence()
    {
        using var f = new LearningPathV4Fixture();
        f.Db.Questions.RemoveRange(await f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101 && q.QuestionId > 10105).ToListAsync()); await f.Db.SaveChangesAsync();
        var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId)); Assert.Equal(5, run.Activities.Count);
        foreach (var a in run.Activities) await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId,
            new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, FinalAnswer = "wrong" });
        var check = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.Purpose == "IndependentCheck");
        Assert.False(check.CanStart); Assert.Contains("0/5", check.RecommendationReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EvidenceOutcome_AddsOneScheduledReviewOrRepair_WithReason_AndNoGetWrites(bool pass)
    {
        using var f = new LearningPathV4Fixture(); var mission = await f.Activate("IndependentCheck", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        foreach (var a in run.Activities)
        {
            var qid = a.Question!.Question!.QuestionId;
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new SubmitActivityRequest {
                ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = pass ? $"answer-{qid % 100}" : "wrong" });
        }
        foreach (var a in await f.Db.Attempts.ToListAsync()) { a.Status = AttemptStatus.Completed; await f.Assess(a); }
        foreach (var job in await f.Db.AIAnalysisJobs.ToListAsync()) job.Status = AIJobStatus.Completed; await f.Db.SaveChangesAsync();
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        var count = await f.Db.LearningPathSessions.CountAsync();
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        Assert.Equal(count, await f.Db.LearningPathSessions.CountAsync()); Assert.NotEmpty(agenda.RecentChanges);
        var next = agenda.Missions.Single(m => pass ? m.Purpose == "Review" : m.Title.StartsWith("Rèn lại"));
        if (pass) { Assert.False(next.CanStart); Assert.True(next.AvailableFrom > DateTime.UtcNow.AddDays(1));
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(next.MissionId))); }
        else Assert.True(next.CanStart);
    }
}
