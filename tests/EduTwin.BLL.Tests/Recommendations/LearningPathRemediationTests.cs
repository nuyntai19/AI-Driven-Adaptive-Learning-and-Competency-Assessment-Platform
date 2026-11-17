using System.Text.Json;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathRemediationTests
{
    private static Question Question(ulong id, string focus) => new() {
        QuestionId = id, Difficulty = 2, GradingCriteria = new() { LearningFocuses = [focus] }
    };

    [Fact]
    public void Focuses_UseExplicitObjectives_NotDistractorsOrSolutions_AndMatchLongestTenseFirst()
    {
        var question = Question(1, "Present simple");
        question.ExpectedReasoning = "Use present perfect continuous";
        question.GradingCriteria.CommonErrors = ["Using past perfect"];
        question.Solution = "Present continuous";
        Assert.Equal(["Hiện tại đơn"], LearningPathRemediation.Focuses(question));
        Assert.Equal(["Present simple"], EduTwin.BLL.CurriculumAndQuestions.QuestionOptionMisconceptionStore.Apply(question.GradingCriteria, []).LearningFocuses);
        question.GradingCriteria.LearningFocuses.Clear();
        Assert.Equal(["Hiện tại hoàn thành tiếp diễn"], LearningPathRemediation.Focuses(question));
        question.ExpectedReasoning = null;
        Assert.Empty(LearningPathRemediation.Focuses(question));
    }

    [Fact]
    public void Selector_BalancesWeakParts_PrefersNewVariants_AndKeepsAContrastQuestion()
    {
        var pool = Enumerable.Range(1, 8).Select(id => Question((ulong)id, id % 2 == 0 ? "A" : "B"))
            .Concat(Enumerable.Range(9, 4).Select(id => Question((ulong)id, "C"))).ToList();
        var selected = LearningPathRemediation.Select(pool, ["A", "B"], [1], [1], 2, 6, new Random(42));
        Assert.Equal(6, selected.Count);
        Assert.Equal(5, selected.Count(question => LearningPathRemediation.Focuses(question).Any(focus => focus is "A" or "B")));
        Assert.Contains(selected, question => LearningPathRemediation.Focuses(question).Contains("A"));
        Assert.Contains(selected, question => LearningPathRemediation.Focuses(question).Contains("B"));
        Assert.DoesNotContain(selected, question => question.QuestionId == 1);
        Assert.Equal(6, selected.Select(question => question.QuestionId).Distinct().Count());
    }

    [Fact]
    public void SmallTargetBank_ShortensTheSet_InsteadOfSubstitutingUnrelatedQuestions()
    {
        var selected = LearningPathRemediation.Select([Question(1, "A"), Question(2, "B"), Question(3, "B"), Question(4, "B")],
            ["A"], [], [], 2, 6, new Random(1));
        Assert.Single(selected);
        Assert.Contains(selected, question => question.QuestionId == 1);
    }

    [Fact]
    public void Readiness_RequiresEachFocusAndDistinctQuestions_AndDiscountsHints()
    {
        var answers = new[] {
            new LearningPathRemediation.Observation(1, ["A"], true, false),
            new LearningPathRemediation.Observation(2, ["A"], true, false),
            new LearningPathRemediation.Observation(3, ["A"], true, false)
        };
        Assert.True(LearningPathRemediation.Secure(["A"], answers));
        Assert.False(LearningPathRemediation.Secure(["A", "B"], answers));
        Assert.False(LearningPathRemediation.Secure(["A"], [answers[0], answers[0], answers[0]]));
        Assert.False(LearningPathRemediation.Secure(["A"], answers.Select(answer => answer with { Hinted = true })));
    }

    [Theory]
    [InlineData(1, 12, 6, 1, 0)]
    [InlineData(2, 12, 6, 2, 0)]
    [InlineData(3, 12, 6, 3, 0)]
    [InlineData(4, 12, 6, 4, 1)]
    [InlineData(8, 12, 6, 5, 1)]
    [InlineData(8, 12, 2, 2, 0)]
    [InlineData(0, 12, 6, 0, 0)]
    public void Composition_NeverDilutesWeakPractice_InShortOrScarceSets(int weak, int other, int size, int expectedWeak, int expectedOther)
    {
        Assert.Equal((expectedWeak, expectedOther), LearningPathRemediation.Composition(weak, other, size));
    }

    [Fact]
    public void Labels_SeparateMainSetsFromRepairRounds_AndExplainTheOrigin()
    {
        var main1 = new ProposedMissionDto { MissionId = "main1", SkillId = "A", Purpose = "Practice", SetNumber = 1, Title = "Rèn kỹ năng: Thì · bộ 1" };
        var main2 = new ProposedMissionDto { MissionId = "main2", SkillId = "A", Purpose = "Practice", SetNumber = 2, Title = "Rèn kỹ năng: Thì · bộ 2" };
        var branch = new ProposedMissionDto { MissionId = "branch1", SkillId = "A", Purpose = "Practice", SetNumber = 3, RemediationRound = 1,
            Title = "Rèn lại: Hiện tại tiếp diễn · vòng 1 · bộ 3", RemediationFocuses = ["Hiện tại tiếp diễn"], RemediationSourceMissionId = "main2" };
        var follow = new ProposedMissionDto { MissionId = "branch2", SkillId = "A", Purpose = "Practice", SetNumber = 4, RemediationRound = 2,
            RemediationFocuses = ["Hiện tại tiếp diễn"], RemediationSourceMissionId = "branch1" };
        var next = new ProposedMissionDto { MissionId = "main3", SkillId = "A", Purpose = "Practice", SetNumber = 5, Title = "Rèn kỹ năng: Thì · bộ 5" };
        var missions = new[] { main1, main2, branch, follow, next };
        var numbers = LearningPathRemediation.MainSetNumbers(missions);
        Assert.Equal(3u, numbers[next.MissionId]);
        Assert.DoesNotContain(branch.MissionId, numbers.Keys);
        Assert.Equal("Rèn lại bằng bài tập: Hiện tại tiếp diễn · vòng 1", LearningPathRemediation.DisplayTitle(branch, numbers));
        Assert.Equal("Rèn kỹ năng: Thì · bộ 3", LearningPathRemediation.DisplayTitle(next, numbers));
        Assert.Equal("Nhánh bổ trợ sau bộ luyện chính 2", LearningPathRemediation.OriginLabel(branch, missions.ToDictionary(m => m.MissionId), numbers));
        Assert.Equal("Tiếp nối bài tập phần yếu vòng 1", LearningPathRemediation.OriginLabel(follow, missions.ToDictionary(m => m.MissionId), numbers));
    }

    [Fact]
    public async Task LegacyBranch_ShowsRepairExercisesWithoutMainNumber_AndActuallyIssuesOnlyTheWeakQuestion_WhenScarce()
    {
        using var fixture = new LearningPathV4Fixture();
        foreach (var question in await fixture.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync())
            question.GradingCriteria = new() { LearningFocuses = [question.QuestionId == 10101 ? "Hiện tại tiếp diễn" : "Hiện tại đơn"] };
        await fixture.Db.SaveChangesAsync();
        var original = await fixture.Activate("Practice", "101");
        await Finish(fixture, original.MissionId, true);
        var plan = await Plan(fixture);
        var branch = plan.Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && m.RemediationRound == 1);
        branch.Title = "Rèn lại: Hiện tại tiếp diễn · vòng 1 · bộ 3";
        var path = await fixture.Db.LearningPaths.SingleAsync(p => p.Status == LearningPathStatus.Active);
        path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json);
        await fixture.Db.SaveChangesAsync();
        var agenda = await fixture.Planner.GetLearningAgendaAsync(fixture.Center, fixture.Student, fixture.English);
        var item = Assert.Single(agenda.Missions, m => m.MissionId == branch.MissionId);
        Assert.Equal(0u, item.SetNumber);
        Assert.Equal(0u, item.EstimatedSets);
        Assert.Equal(1u, item.QuestionCount);
        Assert.Equal("Nhánh bổ trợ sau bộ luyện chính 1", item.RemediationOriginLabel);
        Assert.Contains("bằng bài tập", item.Title);
        Assert.DoesNotContain("bộ 3", item.Title);
        var run = await fixture.Execution.StartMissionAsync(fixture.Center, fixture.Student, Guid.Parse(branch.MissionId));
        var activity = Assert.Single(run.Activities);
        Assert.Equal("Practice", activity.ActivityType);
        Assert.Equal(10101ul, activity.Question!.Question!.QuestionId);
        Assert.DoesNotContain("bộ 3", run.Title);
    }
    [Fact]
    public void CheckGate_ProtectsUnfinishedRepair_ButPreservesPlacementAndDiagnosticPolicies()
    {
        var branch = new ProposedMissionDto { MissionId = "branch", SkillId = "101", Purpose = "Practice", RemediationRound = 1 };
        var check = new ProposedMissionDto { MissionId = "check", SkillId = "101", Purpose = "IndependentCheck" };
        var plan = new PathProposalDto { Milestones = [new() { Missions = [branch, check] }] };
        Assert.NotNull(LearningPathRemediation.CheckBlockReason(plan, check));
        Assert.Null(LearningPathRemediation.CheckBlockReason(plan, branch));
        plan.Milestones[0].ReadyForCheck = true;
        Assert.Null(LearningPathRemediation.CheckBlockReason(plan, check));
        plan.Milestones[0].ReadyForCheck = false;
        plan.IsPlacement = true;
        Assert.Null(LearningPathRemediation.CheckBlockReason(plan, check));
        plan.IsPlacement = false;
        check.IsProbe = true;
        Assert.Null(LearningPathRemediation.CheckBlockReason(plan, check));
    }

    [Theory]
    [InlineData("FOCUS_REPAIR_SUPPORT", true, "NeedsQuestions")]
    [InlineData("FOCUS_REPAIR_NEEDS_QUESTIONS", true, "NeedsQuestions")]
    [InlineData("FOCUS_REPAIR_SUPPORT", false, "NeedsSupport")]
    [InlineData("FOCUS_REPAIR_BRANCH", false, "NeedsPractice")]
    [InlineData("FOCUS_REPAIR_RETURN", false, "ReturnedToMain")]
    public void Progress_ExplainsCurrentAndLegacyFollowUpDecisions(string code, bool missing, string expected)
        => Assert.Equal(expected, LearningPathRemediation.ProgressStatus(code, missing));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScarceBranch_ExplainsWhyThereIsNoNextSet_AndCannotStartFinalCheck(bool answerWrong)
    {
        using var fixture = new LearningPathV4Fixture();
        foreach (var question in await fixture.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync())
            question.GradingCriteria = new() { LearningFocuses = [question.QuestionId == 10101 ? "Hiện tại tiếp diễn" : "Hiện tại đơn"] };
        await fixture.Db.SaveChangesAsync();
        var original = await fixture.Activate("Practice", "101");
        await Finish(fixture, original.MissionId, true);
        var branch = (await Plan(fixture)).Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && m.RemediationRound == 1);
        await Finish(fixture, branch.MissionId, answerWrong);
        var plan = await Plan(fixture);
        Assert.False(plan.Milestones.Single().ReadyForCheck);
        Assert.DoesNotContain(plan.Milestones.Single().Missions, m => m.Purpose == "Practice" && m.RemediationRound == 2);
        var agenda = await fixture.Planner.GetLearningAgendaAsync(fixture.Center, fixture.Student, fixture.English);
        var completedBranch = Assert.Single(agenda.Missions, m => m.MissionId == branch.MissionId);
        Assert.Equal("NeedsQuestions", completedBranch.RemediationStatus);
        Assert.Contains("3 câu", completedBranch.Advice);
        var check = Assert.Single(agenda.Missions, m => m.Purpose == "IndependentCheck");
        Assert.False(check.CanStart);
        Assert.False(check.CanRetry);
        var runCount = await fixture.Db.LearningPathSessionRuns.CountAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Execution.StartMissionAsync(fixture.Center, fixture.Student, Guid.Parse(check.MissionId)));
        Assert.Contains("Nhánh ôn", error.Message);
        Assert.Equal(runCount, await fixture.Db.LearningPathSessionRuns.CountAsync());
    }

    [Fact]
    public async Task WrongRepairWithEnoughQuestions_CreatesRoundsTwoAndThree_AndKeepsCheckLocked()
    {
        using var fixture = new LearningPathV4Fixture();
        foreach (var question in await fixture.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync())
            question.GradingCriteria = new() { LearningFocuses = ["Hiện tại tiếp diễn"] };
        await fixture.Db.SaveChangesAsync();
        var original = await fixture.Activate("Practice", "101");
        await Finish(fixture, original.MissionId, true);
        var first = (await Plan(fixture)).Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && m.RemediationRound == 1);
        await Finish(fixture, first.MissionId, true);
        var second = (await Plan(fixture)).Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && m.RemediationRound == 2);
        Assert.Equal(first.MissionId, second.RemediationSourceMissionId);
        await Finish(fixture, second.MissionId, true);
        var third = (await Plan(fixture)).Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && m.RemediationRound == 3);
        Assert.Equal(second.MissionId, third.RemediationSourceMissionId);
        var agenda = await fixture.Planner.GetLearningAgendaAsync(fixture.Center, fixture.Student, fixture.English);
        Assert.Contains(agenda.Missions, m => m.MissionId == second.MissionId && m.RemediationStatus == "NeedsPractice");
        Assert.False(Assert.Single(agenda.Missions, m => m.Purpose == "IndependentCheck").CanStart);
    }
    private static async Task<V4SessionEvaluationResultDto> Finish(LearningPathV4Fixture fixture, string missionId, bool missWeak)
    {
        var run = await fixture.Execution.StartMissionAsync(fixture.Center, fixture.Student, Guid.Parse(missionId));
        foreach (var activity in run.Activities)
        {
            var question = await fixture.Db.Questions.SingleAsync(q => q.QuestionId == activity.Question!.Question!.QuestionId);
            var wrong = missWeak && question.GradingCriteria.LearningFocuses.Contains("Hiện tại tiếp diễn");
            await fixture.Execution.SubmitActivityAsync(fixture.Center, fixture.Student, activity.ActivityRunId,
                new SubmitActivityRequest { ClientSubmissionId = Guid.NewGuid(), QuestionId = question.QuestionId, FinalAnswer = wrong ? "wrong" : question.CorrectAnswer });
        }
        foreach (var attempt in await fixture.Db.Attempts.Where(a => a.Status != AttemptStatus.Completed).ToListAsync())
        { attempt.Status = AttemptStatus.Completed; await fixture.Assess(attempt); }
        foreach (var job in await fixture.Db.AIAnalysisJobs.ToListAsync()) job.Status = AIJobStatus.Completed;
        await fixture.Db.SaveChangesAsync();
        return await fixture.Evaluator.EvaluateMissionRunAsync(fixture.Center, fixture.Student, run.SessionRunId);
    }

    private static async Task<PathProposalDto> Plan(LearningPathV4Fixture fixture) => JsonSerializer.Deserialize<PathProposalDto>(
        (await fixture.Db.LearningPaths.SingleAsync(path => path.Status == LearningPathStatus.Active)).PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json)!;

    [Fact]
    public async Task FailedPart_BranchesIntoFocusedStudyAndPractice_ThenReturnsAfterVerifiedPractice()
    {
        using var fixture = new LearningPathV4Fixture();
        foreach (var question in await fixture.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync())
            question.GradingCriteria = new() { LearningFocuses = [question.QuestionId % 2 == 1 ? "Hiện tại tiếp diễn" : "Hiện tại đơn"] };
        await fixture.Db.SaveChangesAsync();
        // Keep enough unseen variants for two repair rounds; repeated exposed
        // answers are deliberately weaker evidence and must not pass this test.
        for (ulong id = 10113; id <= 10132; id++) fixture.Db.Questions.Add(new Question {
            QuestionId = id, CenterId = fixture.Center, SubjectId = fixture.English, PrimaryTopicNodeId = 101,
            QuestionType = QuestionType.ShortAnswer, AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            QuestionText = $"Biến thể #{id}", CorrectAnswer = $"answer-{id}", Solution = "Ví dụ đã biên soạn",
            LanguageCode = "vi", Status = QuestionStatus.Active, MaxScore = 10, Difficulty = 1, GradeLevel = 10,
            GradingCriteria = new() { LearningFocuses = [id % 2 == 1 ? "Hiện tại tiếp diễn" : "Hiện tại đơn"] },
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync();
        var original = await fixture.Activate("Practice", "101");
        await Finish(fixture, original.MissionId, true);
        var plan = await Plan(fixture);
        var branch = Assert.Single(plan.Milestones.SelectMany(m => m.Missions), m => m.Purpose == "Practice" && m.RemediationRound == 1);
        Assert.Equal(["Hiện tại tiếp diễn"], branch.RemediationFocuses);
        Assert.Equal(original.MissionId, branch.RemediationSourceMissionId);
        Assert.Contains(plan.Milestones.SelectMany(m => m.Missions), m => m.Purpose == "Study" && m.RemediationRound == 1);
        var result = await Finish(fixture, branch.MissionId, false);
        Assert.DoesNotContain("Quay lại bộ luyện tổng hợp", result.NextStep);
        plan = await Plan(fixture);
        var second = Assert.Single(plan.Milestones.SelectMany(m => m.Missions), m => m.Purpose == "Practice" && m.RemediationRound == 2);
        result = await Finish(fixture, second.MissionId, false);
        var diagnosticPlan = await Plan(fixture);
        var secondId = Guid.Parse(second.MissionId);
        var diagnosticRun = await fixture.Db.LearningPathSessionRuns.Include(r => r.Session).SingleAsync(r => r.SessionId == secondId);
        var diagnostic = await LearningPathRemediation.ObserveAsync(fixture.Db, diagnosticRun, second, diagnosticPlan.Milestones.Single().Missions, default);
        Assert.True(result.NextStep?.Contains("Quay lại bộ luyện tổng hợp") == true,
            $"Accuracy={result.PracticeAccuracy}; priorStreak={second.PassStreak}; current={JsonSerializer.Serialize(diagnostic.Current)}; recent={JsonSerializer.Serialize(diagnostic.History.Take(8))}");
        Assert.False((await Plan(fixture)).Milestones.Single().ReadyForCheck);
        var agenda = await fixture.Planner.GetLearningAgendaAsync(fixture.Center, fixture.Student, fixture.English);
        Assert.Contains(agenda.Missions, m => m.RemediationRound == 1 && m.RemediationFocuses.Contains("Hiện tại tiếp diễn"));
        Assert.All(agenda.Missions.Where(m => m.Purpose == "Study" && m.RemediationRound > 0), m => Assert.Equal("Skipped", m.Status));
    }

    [Fact]
    public async Task RepeatedFailure_StopsAutomaticDrilling_WithoutMarkingReadyOrDuplicatingFollowUp()
    {
        using var fixture = new LearningPathV4Fixture();
        foreach (var question in await fixture.Db.Questions.Where(q => q.PrimaryTopicNodeId == 101).ToListAsync())
            question.GradingCriteria = new() { LearningFocuses = ["Hiện tại tiếp diễn"] };
        await fixture.Db.SaveChangesAsync();
        var original = await fixture.Activate("Practice", "101");
        await Finish(fixture, original.MissionId, true);
        var plan = await Plan(fixture);
        var branch = plan.Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && m.RemediationRound == 1);
        branch.RemediationRound = LearningPathRemediation.MaxRounds;
        var path = await fixture.Db.LearningPaths.SingleAsync(p => p.Status == LearningPathStatus.Active);
        path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json);
        await fixture.Db.SaveChangesAsync();
        var result = await Finish(fixture, branch.MissionId, true);
        Assert.Contains("Tạm dừng", result.NextStep);
        Assert.False((await Plan(fixture)).Milestones.Single().ReadyForCheck);
        var before = await fixture.Db.LearningPathSessions.CountAsync();
        var branchId = Guid.Parse(branch.MissionId);
        var run = await fixture.Db.LearningPathSessionRuns.SingleAsync(r => r.SessionId == branchId);
        await fixture.Evaluator.EvaluateMissionRunAsync(fixture.Center, fixture.Student, run.SessionRunId);
        Assert.Equal(before, await fixture.Db.LearningPathSessions.CountAsync());
    }
}