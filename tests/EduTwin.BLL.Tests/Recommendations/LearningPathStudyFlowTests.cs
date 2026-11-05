using System.Text.Json;
using EduTwin.BLL.Recommendations.Planning;
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
    private static async Task<PathProposalDto> ActivateWithBudget(LearningPathV4Fixture f, uint minutes, params string[] skills)
    {
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new() { SubjectId = f.English,
            GoalType = "Foundation", TargetSkillIds = skills.ToList(), AvailableMinutesPerDay = minutes, Schedule = new() { QuestionReuseDelayDays = 0 } });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new() { SubjectId = f.English, GoalId = goal.GoalId });
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId); return proposal;
    }
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
            var answer = (await f.Db.Questions.SingleAsync(q => q.QuestionId == qid)).CorrectAnswer;
            await f.Execution.SubmitActivityAsync(f.Center, f.Student, a.ActivityRunId, new SubmitActivityRequest
            { ClientSubmissionId = Guid.NewGuid(), QuestionId = qid, FinalAnswer = correct ? answer : "wrong" });
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

    private static async Task<AgendaItemDto> NextSet(LearningPathV4Fixture f, string skill)
        => (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60)).Missions
            .Last(m => m.SkillId == skill && m.Purpose == "Practice" && m.Status == "NotStarted" && m.CanStart);

    private static async Task<V4SessionEvaluationResultDto> DoSet(LearningPathV4Fixture f, AgendaItemDto set, bool correct)
    {
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(set.MissionId), new StartMissionRequest { QuestionCount = set.QuestionCount });
        await SubmitAll(f, run, correct);
        await FinishGrading(f);
        return await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
    }

    [Fact]
    public async Task Planner_WeakSkills_GetALongerLoop_StartingEasier_WithFixedSetSize()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 20); AddQuestions(f, 102, 20);
        for (ulong i = 1; i <= 4; i++) await f.RecordEvidence(102, i, correct: false);
        var proposal = await f.Proposal("101", "102");
        Assert.Equal(2, proposal.PacingVersion);
        var all = proposal.Milestones.SelectMany(m => m.Missions).ToList();
        var weak = all.Single(m => m.SkillId == "102" && m.Purpose == "Practice");
        var other = all.Single(m => m.SkillId == "101" && m.Purpose == "Practice");
        Assert.True(weak.EstimatedSets > other.EstimatedSets, $"{weak.EstimatedSets} vs {other.EstimatedSets}");
        Assert.True(weak.TargetDifficulty < other.TargetDifficulty);
        Assert.All(all.Where(m => m.Purpose == "Practice"), m => Assert.Equal(LearningPathPacing.SetSize, m.QuestionCount));
        Assert.Equal(10u, all.Single(m => m.SkillId == "102" && m.Purpose == "Study").EstimatedMinutes);
        Assert.Equal("102", proposal.Milestones.First().SkillIds.Single());
    }

    [Fact]
    public async Task Agenda_MoreTime_AddsMoreSets_NotLongerSets_AndWeakSkillsGetAtLeastAsMany()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 30); AddQuestions(f, 102, 30);
        for (ulong i = 1; i <= 4; i++) await f.RecordEvidence(102, i, correct: false);
        await ActivateWithBudget(f, 180, "101", "102");
        var shortDay = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 15);
        var longDay = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 180);
        Assert.Equal(180u, (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 999)).AvailableMinutes);
        Assert.InRange(longDay.PlannedMinutes, 1u, 180u);
        Assert.True(longDay.PlannedQuestionCount > shortDay.PlannedQuestionCount);
        Assert.All(longDay.TodayPlan.Concat(shortDay.TodayPlan).Where(m => m.Purpose == "Practice"), m =>
        { Assert.InRange(m.QuestionCount, 1u, LearningPathPacing.SetSize); Assert.InRange(m.PlannedSets, 1u, LearningPathPacing.MaxSetsPerSkillPerDay); });
        Assert.InRange(shortDay.PlannedMinutes, 1u, 15u);
        var weak = longDay.TodayPlan.Single(m => m.SkillId == "102" && m.Purpose == "Practice").PlannedSets;
        var other = longDay.TodayPlan.Single(m => m.SkillId == "101" && m.Purpose == "Practice").PlannedSets;
        Assert.True(weak >= other && weak > 1, $"weak {weak} vs other {other}");
        // The check is not pushed before two passing sets; it stays on the route with advice.
        Assert.DoesNotContain(longDay.TodayPlan, m => m.Purpose == "IndependentCheck");
        Assert.NotNull(longDay.Missions.First(m => m.Purpose == "IndependentCheck").Advice);
        Assert.Equal(2u, longDay.Progress!.TotalSkills);
    }

    [Fact]
    public async Task PracticeLoop_StepsUpAfterAGoodSet_AndRecommendsTheCheckAfterTwo()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 20);
        await ActivateWithBudget(f, 60, "101");
        var first = await DoSet(f, await NextSet(f, "101"), correct: true);
        Assert.Equal(1m, first.PracticeAccuracy);
        Assert.Contains("khó hơn", first.NextStep);
        var second = await DoSet(f, await NextSet(f, "101"), correct: true);
        Assert.Contains("sẵn sàng tự kiểm tra", second.NextStep);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60);
        Assert.DoesNotContain(agenda.Missions, m => m.Purpose == "Practice" && m.Status == "NotStarted");
        var check = agenda.Missions.Single(m => m.Purpose == "IndependentCheck");
        Assert.Null(check.Advice);
        Assert.Contains(agenda.TodayPlan, m => m.Purpose == "IndependentCheck");
        Assert.Equal(1u, agenda.Progress!.ReadyForCheckSkills);
    }

    [Fact]
    public async Task PracticeLoop_AfterAWeakSet_StepsDown_AndPointsBackToTheLesson()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 20);
        await f.Activate("Practice", "101");
        var result = await DoSet(f, await NextSet(f, "101"), correct: false);
        Assert.Equal(0m, result.PracticeAccuracy);
        Assert.Contains("Học lại", result.NextStep);
        var next = await NextSet(f, "101");
        Assert.Contains("bộ 2", next.Title);
        var plan = JsonSerializer.Deserialize<PathProposalDto>((await f.Db.LearningPaths.SingleAsync(p => p.Status == LearningPathStatus.Active)).PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json)!;
        Assert.Equal((byte?)1, plan.Milestones.SelectMany(m => m.Missions).Single(m => m.MissionId == next.MissionId).TargetDifficulty);
    }

    [Fact]
    public async Task QuickTry_ForAnUnknownSkill_SkipsTheLessonWhenPassed()
    {
        using var f = new LearningPathV4Fixture();
        AddQuestions(f, 101, 20);
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest { SubjectId = f.English, GoalType = "KeepUp",
            TargetSkillIds = ["101"], AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 5 });
        var proposal = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student, new GeneratePathProposalRequest { SubjectId = f.English, GoalId = goal.GoalId });
        var probe = proposal.Milestones.Single().Missions.First();
        Assert.True(probe.IsProbe); Assert.Equal(LearningPathPacing.ProbeSize, probe.QuestionCount);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, proposal.ProposalId);
        var agendaItem = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60)).Missions.Single(m => m.MissionId == probe.MissionId);
        await DoSet(f, agendaItem, correct: true);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60);
        Assert.Equal("Skipped", agenda.Missions.Single(m => m.Purpose == "Study").Status);
        Assert.Contains(agenda.Missions, m => m.Purpose == "Practice" && m.Title.Contains("bộ 2"));
    }

    [Fact]
    public async Task FailedReview_SendsAConfirmedSkillBackToPractice()
    {
        using var f = new LearningPathV4Fixture();
        var proposal = await ActivateWithBudget(f, 60, "103");
        var mission = proposal.Milestones.SelectMany(m => m.Missions).Single(m => m.Purpose == "IndependentCheck");
        var check = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        await SubmitAll(f, check, correct: true);
        await FinishGrading(f);
        Assert.Equal("Passed", (await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, check.SessionRunId)).Outcome);
        // Bring the scheduled review forward instead of waiting two days.
        var path = await f.Db.LearningPaths.SingleAsync(p => p.Status == LearningPathStatus.Active);
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json)!;
        var review = plan.Milestones.SelectMany(m => m.Missions).Single(m => m.Purpose == "Review");
        review.AvailableFrom = DateTime.UtcNow.AddMinutes(-1);
        path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json); await f.Db.SaveChangesAsync();

        var reviewRun = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(review.MissionId));
        await SubmitAll(f, reviewRun, correct: false);
        await FinishGrading(f);
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, reviewRun.SessionRunId);
        Assert.Contains("củng cố lại", result.NextStep);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60);
        Assert.Contains(agenda.TodayPlan, m => m.Purpose == "Practice" && m.Title.StartsWith("Rèn lại"));
        Assert.DoesNotContain(agenda.Missions, m => m.Purpose == "Review" && m.Status == "NotStarted");
    }

    [Fact]
    public async Task IndependentCheck_MixesDifficulty_WhenTheBankAllows()
    {
        using var f = new LearningPathV4Fixture();
        foreach (var q in await f.Db.Questions.Where(q => q.PrimaryTopicNodeId == 103).ToListAsync())
            q.Difficulty = q.QuestionId % 100 <= 4 ? (byte)1 : q.QuestionId % 100 <= 8 ? (byte)3 : (byte)4;
        await f.Db.SaveChangesAsync();
        var mission = await f.Activate("IndependentCheck", "103");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId));
        var ids = run.Activities.Select(a => a.Question!.Question!.QuestionId).ToList();
        Assert.Equal(5, ids.Count);
        var levels = await f.Db.Questions.Where(q => ids.Contains(q.QuestionId)).ToDictionaryAsync(q => q.QuestionId, q => q.Difficulty);
        Assert.Equal(new byte[] { 1, 1, 3, 3, 4 }, ids.Select(id => levels[id]));
    }

    [Fact]
    public async Task HintedCorrectAnswers_CountHalf()
    {
        using var f = new LearningPathV4Fixture();
        var mission = await f.Activate("Practice", "101");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(mission.MissionId), new StartMissionRequest { QuestionCount = 2 });
        await f.Execution.RequestAssistanceAsync(f.Center, f.Student, run.Activities[0].ActivityRunId, new RequestAssistanceRequest());
        await SubmitAll(f, run, correct: true);
        await FinishGrading(f);
        var result = await f.Evaluator.EvaluateMissionRunAsync(f.Center, f.Student, run.SessionRunId);
        Assert.Equal(.75m, result.PracticeAccuracy);
        var insight = (await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English)).Insights.Skills.Single(s => s.SkillId == "101");
        Assert.Equal(1u, insight.HintedCorrectCount); Assert.Equal(2u, insight.CorrectCount);
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
