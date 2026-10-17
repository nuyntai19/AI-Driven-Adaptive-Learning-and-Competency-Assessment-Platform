using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

/// <summary>Hints, pause/cancel, retry, weakness-based sizing and the daily time budget.</summary>
public sealed class LearningPathStudyFlowTests
{
    private static async Task FinishGrading(LearningPathV4Fixture f)
    {
        foreach (var a in await f.Db.Attempts.Where(a => a.Status != AttemptStatus.Completed).ToListAsync()) { a.Status = AttemptStatus.Completed; await f.Assess(a); }
        foreach (var j in await f.Db.AIAnalysisJobs.ToListAsync()) j.Status = AIJobStatus.Completed;
        await f.Db.SaveChangesAsync();
    }

    private static async Task SubmitAll(LearningPathV4Fixture f, MissionRunDto run, bool correct)
    {
        foreach (var a in run.Activities)
        {
            var qid = a.Question!.Question!.QuestionId;
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new SubmitActivityRequest
            { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = correct ? $"answer-{qid % 100}" : "wrong" });
        }
    }

    private static void AddQuestions(LearningPathV4Fixture f, ulong topic, int extra)
    {
        var now = DateTime.UtcNow;
        for (ulong i = 1; i <= (ulong)extra; i++) f.Db.Questions.Add(new Question { QuestionId = topic * 1000 + i, CenterId = f.Center, SubjectId = f.English,
            PrimaryTopicNodeId = topic, QuestionType = QuestionType.ShortAnswer, AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            QuestionText = $"Cau bo sung {topic} #{i}", CorrectAnswer = $"extra-{i}", Solution = "Loi giai bo sung", LanguageCode = "vi",
            Status = QuestionStatus.Active, MaxScore = 10, Difficulty = 2, CreatedAt = now, UpdatedAt = now });
        f.Db.SaveChanges();
    }

    [Fact]
    public async Task Hints_AreRevealedStepByStep_AndNeverContainTheAnswer()
    {
        using var f = new LearningPathV4Fixture();
        var q = await f.Db.Questions.SingleAsync(x => x.QuestionId == 10101);
        q.ExpectedReasoning = "Thoi quen hang ngay dung hien tai don; dap an answer-1 chia theo chu ngu.";
        q.Solution = "Buoc 1: xac dinh chu ngu I.\nBuoc 2: hien tai don voi I dung dong tu nguyen mau.\nVay dap an la answer-1.";
        await f.Db.SaveChangesAsync();
        var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var first = run.Activities[0];
        Assert.Equal(10101ul, first.Question!.Question!.QuestionId);

        var h1 = await f.Execution.RequestAssistanceAsync(f.Center, f.Student, first.ActivityRunId, new RequestAssistanceRequest { StepIndex = 0 });
        var h2 = await f.Execution.RequestAssistanceAsync(f.Center, f.Student, first.ActivityRunId, new RequestAssistanceRequest { StepIndex = 1 });
        var h3 = await f.Execution.RequestAssistanceAsync(f.Center, f.Student, first.ActivityRunId, new RequestAssistanceRequest { StepIndex = 2 });
        Assert.Equal(3u, h1.TotalHints); Assert.True(h1.HasMoreHints); Assert.False(h3.HasMoreHints);
        Assert.Equal("Định hướng", h1.HintTitle); Assert.Contains("___", h1.Explanation);
        Assert.Contains("Buoc 1", h2.Explanation); Assert.DoesNotContain("Buoc 2", h2.Explanation);
        Assert.Contains("Buoc 2", h3.Explanation); Assert.DoesNotContain("Vay dap an", h3.Explanation);
        Assert.All(new[] { h1, h2, h3 }, h => { Assert.False(h.IsBlocked); Assert.DoesNotContain("answer-1", h.Explanation); });

        // Re-opening an earlier hint after a reload does not count as extra help.
        await f.Execution.RequestAssistanceAsync(f.Center, f.Student, first.ActivityRunId, new RequestAssistanceRequest { StepIndex = 0 });
        Assert.Equal(3u, (await f.Execution.GetActivityRunAsync(f.Center, f.Student, first.ActivityRunId)).AssistanceRequestedCount);
    }

    [Fact]
    public async Task Hints_WithoutAuthoredSolution_GiveOnlyAnHonestApproach()
    {
        using var f = new LearningPathV4Fixture();
        foreach (var q in await f.Db.Questions.Where(x => x.PrimaryTopicNodeId == 101).ToListAsync()) q.Solution = "";
        await f.Db.SaveChangesAsync();
        var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var hint = await f.Execution.RequestAssistanceAsync(f.Center, f.Student, run.Activities[0].ActivityRunId, new RequestAssistanceRequest());
        Assert.Equal(1u, hint.TotalHints); Assert.False(hint.HasMoreHints);
        Assert.True(hint.Explanation.Contains("Hiện tại đơn"), hint.Explanation);
    }

    [Fact]
    public async Task CancelPractice_KeepsSubmittedAnswers_AndTheMissionCanStartAgain()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("Practice", "101");
        var id = Guid.Parse(mission.MissionId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, id);
        var a = run.Activities[0]; var qid = a.Question!.Question!.QuestionId;
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new SubmitActivityRequest
        { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = $"answer-{qid % 100}" });

        var cancelled = await f.Execution.AbandonMissionRunAsync(f.Center, f.Student, id, run.SessionRunId);
        Assert.Equal("Abandoned", cancelled.Status);
        Assert.Equal(1, await f.Db.Attempts.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.SaveDraftAsync(f.Center, f.Student, run.Activities[1].ActivityRunId,
            new SaveActivityDraftRequest { ExpectedRevision = 1, DraftPayload = "late" }));

        var again = await f.Execution.StartMissionAsync(f.Center, f.Student, id);
        Assert.NotEqual(run.SessionRunId, again.SessionRunId);
        Assert.Equal("InProgress", again.Status);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        var item = agenda.Missions.Single(m => m.MissionId == mission.MissionId);
        Assert.Equal(again.SessionRunId, item.ActiveRunId);
        Assert.Contains(item.Runs, r => r.Status == "Abandoned" && r.AnsweredQuestions == 1);
    }

    [Fact]
    public async Task CancelIndependentCheck_AfterAnAnswer_IsRefused_ButBeforeAnswering_IsAllowed()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("IndependentCheck", "103");
        var id = Guid.Parse(mission.MissionId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, id);
        await f.Execution.AbandonMissionRunAsync(f.Center, f.Student, id, run.SessionRunId);
        var second = await f.Execution.StartMissionAsync(f.Center, f.Student, id);
        var a = second.Activities[0];
        await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new SubmitActivityRequest
        { ClientSubmissionId = Guid.NewGuid(), QuestionId = a.Question!.Question!.QuestionId, FinalAnswer = "wrong" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.AbandonMissionRunAsync(f.Center, f.Student, id, second.SessionRunId));
    }

    [Fact]
    public async Task FinishedPractice_OpensForReview_AndRetryStartsANewRunWithHistory()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("Practice", "101");
        var id = Guid.Parse(mission.MissionId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, id, new StartMissionRequest { QuestionCount = 3 });
        Assert.Equal(3, run.Activities.Count);
        await SubmitAll(f, run, correct: false);
        await FinishGrading(f);
        Assert.Equal("ActivityOnlyCompleted", (await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId)).Outcome);

        Assert.Equal(run.SessionRunId, (await f.Execution.StartMissionAsync(f.Center, f.Student, id)).SessionRunId);
        var before = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.MissionId == mission.MissionId);
        Assert.True(before.CanRetry); Assert.Equal("Luyện thêm", before.RetryLabel);

        var retry = await f.Execution.StartMissionAsync(f.Center, f.Student, id, new StartMissionRequest { Retry = true, QuestionCount = 4 });
        Assert.NotEqual(run.SessionRunId, retry.SessionRunId);
        // Missed questions come back first.
        var missed = run.Activities.Select(a => a.Question!.Question!.QuestionId).ToHashSet();
        Assert.All(retry.Activities.Take(3), a => Assert.Contains(a.Question!.Question!.QuestionId, missed));
        var item = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.MissionId == mission.MissionId);
        Assert.Equal(2, item.Runs.Count);
        Assert.Equal(0u, item.Runs[0].CorrectAnswers); Assert.Equal(3u, item.Runs[0].AnsweredQuestions);
    }

    [Fact]
    public async Task FailedIndependentCheck_CanBeRetaken_OnlyAfterTheRepairSet()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("IndependentCheck", "103");
        var id = Guid.Parse(mission.MissionId);
        var check = await f.Execution.StartMissionAsync(f.Center, f.Student, id);
        await SubmitAll(f, check, correct: false);
        await FinishGrading(f);
        Assert.Equal("NeedsSupport", (await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, check.SessionRunId)).Outcome);

        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        var item = agenda.Missions.Single(m => m.MissionId == mission.MissionId);
        Assert.False(item.CanRetry); Assert.NotNull(item.RetryBlockedReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Execution.StartMissionAsync(f.Center, f.Student, id, new StartMissionRequest { Retry = true }));

        var repair = agenda.Missions.Single(m => m.Title.StartsWith("Rèn lại"));
        Assert.True(repair.QuestionCount >= 6);
        var repairRun = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(repair.MissionId), new StartMissionRequest { QuestionCount = repair.QuestionCount });
        await SubmitAll(f, repairRun, correct: true);
        await FinishGrading(f);
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, repairRun.SessionRunId);

        var after = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Missions.Single(m => m.MissionId == mission.MissionId);
        Assert.True(after.CanRetry, after.RetryBlockedReason);
        var retake = await f.Execution.StartMissionAsync(f.Center, f.Student, id, new StartMissionRequest { Retry = true });
        Assert.NotEqual(check.SessionRunId, retake.SessionRunId);
        var seen = check.Activities.Concat(repairRun.Activities).Select(a => a.Question!.Question!.QuestionId).ToHashSet();
        Assert.All(retake.Activities, a => Assert.DoesNotContain(a.Question!.Question!.QuestionId, seen));
    }

    [Fact]
    public async Task Planner_GivesWeakSkillsMoreQuestions_SplitIntoTwoSets()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 20); AddQuestions(f, 102, 20);
        for (ulong i = 1; i <= 4; i++) await f.RecordEvidence(102, i, correct: false);
        var proposal = await f.Proposal("101", "102");
        int Practice(string skill) => proposal.Milestones.SelectMany(m => m.Missions).Where(m => m.SkillId == skill && m.Purpose == "Practice").Sum(m => (int)m.QuestionCount);
        var weakSets = proposal.Milestones.SelectMany(m => m.Missions).Count(m => m.SkillId == "102" && m.Purpose == "Practice");
        var otherSets = proposal.Milestones.SelectMany(m => m.Missions).Count(m => m.SkillId == "101" && m.Purpose == "Practice");
        Assert.Equal(2, weakSets); Assert.Equal(1, otherSets);
        Assert.True(Practice("102") > Practice("101"), $"weak {Practice("102")} vs other {Practice("101")}");
        Assert.Equal("102", proposal.Milestones.First().SkillIds.Single());
    }

    [Fact]
    public async Task Agenda_MoreTime_PlansMoreQuestions_WithinTheBudget_AndSpareTimeGoesToTheWeakestSkill()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 30); AddQuestions(f, 102, 30);
        for (ulong i = 1; i <= 4; i++) await f.RecordEvidence(102, i, correct: false);
        await f.Activate("Practice", "101", "102");
        var shortDay = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 15);
        var longDay = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 180);
        var max = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 999);
        Assert.Equal(300u, max.AvailableMinutes);
        Assert.InRange(shortDay.PlannedMinutes, 1u, 15u);
        Assert.InRange(longDay.PlannedMinutes, 1u, 180u);
        Assert.True(longDay.PlannedQuestionCount > shortDay.PlannedQuestionCount);
        Assert.Equal(longDay.TodayPlan.First().MissionId, longDay.PrimaryMission!.MissionId);
        var weak = longDay.TodayPlan.Where(m => m.SkillId == "102" && m.Purpose == "Practice").Sum(m => (int)m.QuestionCount);
        var other = longDay.TodayPlan.Where(m => m.SkillId == "101" && m.Purpose == "Practice").Sum(m => (int)m.QuestionCount);
        Assert.True(weak > other, $"weak {weak} vs other {other}");
        Assert.All(longDay.TodayPlan.Where(m => m.Purpose == "Practice"), m => Assert.InRange(m.QuestionCount, 2u, 20u));
        Assert.NotNull(longDay.PacingNote);
    }

    [Fact]
    public async Task ReplacingTheRoute_KeepsEarlierAnswers_AndListsThePreviousRoute()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId), new StartMissionRequest { QuestionCount = 3 });
        await SubmitAll(f, run, correct: false);
        await FinishGrading(f);
        await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        var next = await f.Proposal("101", "102");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, next.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English);
        var history = Assert.Single(agenda.PathHistory);
        Assert.Equal("Superseded", history.Status); Assert.Equal(3u, history.AnsweredQuestions);
        Assert.Equal(3, await f.Db.Attempts.CountAsync());
        Assert.Equal(3u, agenda.Insights.Skills.Single(s => s.SkillId == "101").WrongCount);
    }
}
