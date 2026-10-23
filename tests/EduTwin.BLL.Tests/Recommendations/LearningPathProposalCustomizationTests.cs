using System.Text.Json;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.Contracts.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathProposalCustomizationTests
{
    private static CustomizePathProposalRequest Options(params string[] skills) => new()
    {
        TargetSkillIds = skills.ToList(), AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 3,
        PracticeQuestionsPerSet = 7, DeadlineDate = DateTime.UtcNow.AddDays(30)
    };

    [Fact]
    public async Task CustomizationPreservesDirectionAndMissionOrder_AndDoesNotActivateThePreview()
    {
        using var f = new LearningPathV4Fixture();
        var accepted = await f.Proposal("102");
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, accepted.ProposalId);
        var source = await f.Proposal("101", "103");
        var originalJson = (await f.Db.PathProposals.FindAsync(source.ProposalId))!.CandidatePlan.RootElement.GetRawText();
        var adjusted = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, Options("103"));
        Assert.Equal(source.GoalTitle, adjusted.GoalTitle);
        Assert.NotEqual(source.ProposalId, adjusted.ProposalId);
        var sourceGoal = await f.Db.LearningGoals.FindAsync(source.GoalId);
        var adjustedGoal = await f.Db.LearningGoals.FindAsync(adjusted.GoalId);
        Assert.Equal(sourceGoal!.GoalType, adjustedGoal!.GoalType);
        Assert.Equal(30u, adjusted.AvailableMinutesPerDay); Assert.Equal(3u, adjusted.AvailableDaysPerWeek);
        Assert.Single(adjusted.Milestones);
        var before = source.Milestones.Single(m => m.SkillIds.Contains("103"));
        var after = adjusted.Milestones.Single();
        Assert.Equal(before.Missions.Select(m => m.Purpose), after.Missions.Select(m => m.Purpose));
        Assert.All(after.Missions.Where(m => m.Purpose == "IndependentCheck"), m => Assert.Equal(5u, m.QuestionCount));
        Assert.All(after.Missions.Where(m => m.Purpose == "Practice"), m => Assert.Equal(7u, m.QuestionCount));
        Assert.Equal(accepted.GoalId, (await f.Planner.GetActiveGoalAsync(f.Center, f.Student, f.English))!.GoalId);
        Assert.Equal(originalJson, (await f.Db.PathProposals.FindAsync(source.ProposalId))!.CandidatePlan.RootElement.GetRawText());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public async Task AcceptedConfigurationIsUsedByAgendaExecutionAndFollowingSets(int count)
    {
        using var f = new LearningPathV4Fixture();
        var source = await f.Proposal("101"); var options = Options("101"); options.PracticeQuestionsPerSet = (uint)count;
        var adjusted = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, options);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, adjusted.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60);
        var practice = agenda.Missions.Single(m => m.Purpose == "Practice");
        Assert.Equal((uint)count, practice.QuestionCount);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(practice.MissionId));
        Assert.Equal(count, run.Activities.Count);
        var savedRun = await f.Db.LearningPathSessionRuns.Include(r => r.Session).SingleAsync(r => r.SessionRunId == run.SessionRunId);
        await LearningPathFollowUp.AddAsync(f.Db, savedRun, "ActivityOnlyCompleted", DateTime.UtcNow, default, .9m);
        var path = await f.Db.LearningPaths.SingleAsync(p => p.LearningPathId == savedRun.Session.LearningPathId);
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal((uint)count, plan.Milestones.Single().Missions.Last(m => m.Purpose == "Practice").QuestionCount);
    }

    [Fact]
    public async Task DifferentSectionsKeepDifferentSetSizesInAgendaAndFollowingSets()
    {
        using var f = new LearningPathV4Fixture();
        var source = await f.Proposal("101", "103");
        var options = Options("101", "103");
        options.PracticeQuestionsBySkill = new() { ["101"] = 3, ["103"] = 7 };
        var adjusted = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, options);
        Assert.Equal(3u, adjusted.Milestones.Single(m => m.SkillIds.Contains("101")).Missions.Single(m => m.Purpose == "Practice" && !m.IsProbe).QuestionCount);
        Assert.Equal(7u, adjusted.Milestones.Single(m => m.SkillIds.Contains("103")).Missions.Single(m => m.Purpose == "Practice" && !m.IsProbe).QuestionCount);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, adjusted.ProposalId);
        var agenda = await f.Planner.GetLearningAgendaAsync(f.Center, f.Student, f.English, 60);
        Assert.Equal(3u, agenda.Missions.Single(m => m.SkillId == "101" && m.Purpose == "Practice").QuestionCount);
        Assert.Equal(7u, agenda.Missions.Single(m => m.SkillId == "103" && m.Purpose == "Practice").QuestionCount);
        var practice = agenda.Missions.Single(m => m.SkillId == "103" && m.Purpose == "Practice");
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(practice.MissionId));
        Assert.Equal(7, run.Activities.Count);
        var savedRun = await f.Db.LearningPathSessionRuns.Include(r => r.Session).SingleAsync(r => r.SessionRunId == run.SessionRunId);
        await LearningPathFollowUp.AddAsync(f.Db, savedRun, "ActivityOnlyCompleted", DateTime.UtcNow, default, .9m);
        var path = await f.Db.LearningPaths.SingleAsync(p => p.LearningPathId == savedRun.Session.LearningPathId);
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(7u, plan.Milestones.Single(m => m.SkillIds.Contains("103")).Missions.Last(m => m.Purpose == "Practice").QuestionCount);
    }

    [Fact]
    public async Task QuickProbeKeepsItsOwnSizeAndNextPracticeUsesTheSectionChoice()
    {
        using var f = new LearningPathV4Fixture();
        var goal = await f.Planner.CreateOrUpdateGoalAsync(f.Center, f.Student, new CreateLearningGoalRequest
        {
            SubjectId = f.English, GoalType = "Diagnostic", Title = "Xác định điểm xuất phát",
            TargetSkillIds = ["101"], AvailableMinutesPerDay = 30, AvailableDaysPerWeek = 5
        });
        var source = await f.Planner.GeneratePathProposalAsync(f.Center, f.Student,
            new GeneratePathProposalRequest { GoalId = goal.GoalId, SubjectId = f.English });
        Assert.Contains(source.Milestones.SelectMany(m => m.Missions), m => m.IsProbe);
        var options = Options("101"); options.PracticeQuestionsBySkill = new() { ["101"] = 7 };
        var adjusted = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, options);
        var probe = adjusted.Milestones.Single().Missions.Single(m => m.IsProbe && m.Purpose == "Practice");
        Assert.Equal(4u, probe.QuestionCount);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, adjusted.ProposalId);
        var run = await f.Execution.StartMissionAsync(f.Center, f.Student, Guid.Parse(probe.MissionId));
        Assert.Equal(4, run.Activities.Count);
        var savedRun = await f.Db.LearningPathSessionRuns.Include(r => r.Session).SingleAsync(r => r.SessionRunId == run.SessionRunId);
        await LearningPathFollowUp.AddAsync(f.Db, savedRun, "ActivityOnlyCompleted", DateTime.UtcNow, default, .9m);
        var path = await f.Db.LearningPaths.SingleAsync(p => p.LearningPathId == savedRun.Session.LearningPathId);
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(7u, plan.Milestones.Single().Missions.Single(m => m.Purpose == "Practice" && !m.IsProbe).QuestionCount);
    }

    [Fact]
    public async Task RequiredFoundationIsKeptWhenItsDependentSectionIsSelected()
    {
        using var f = new LearningPathV4Fixture();
        f.AddPrerequisite(101, 103);
        await f.Db.SaveChangesAsync();
        var source = await f.Proposal("103");
        var adjusted = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, Options("103"));
        Assert.Equal(new[] { "101", "103" }, adjusted.Milestones.SelectMany(m => m.SkillIds));
    }

    [Fact]
    public async Task InvalidScopeOwnerAndExpiredOrAcceptedSourcesCannotBeCustomized()
    {
        using var f = new LearningPathV4Fixture(); var source = await f.Proposal("101");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Planner.CustomizePathProposalAsync(f.Center, Guid.NewGuid(), source.ProposalId, Options("101")));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, Options("201")));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, Options()));
        var invalid = Options("101"); invalid.PracticeQuestionsPerSet = 21;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, invalid));
        invalid = Options("101"); invalid.PracticeQuestionsBySkill = new() { ["101"] = 21 };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, invalid));
        invalid = Options("101"); invalid.PracticeQuestionsBySkill = new() { ["201"] = 5 };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, invalid));
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, source.ProposalId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, Options("101")));
        var expired = await f.Proposal("101");
        (await f.Db.PathProposals.FindAsync(expired.ProposalId))!.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Planner.CustomizePathProposalAsync(f.Center, f.Student, expired.ProposalId, Options("101")));
    }

    [Fact]
    public async Task SourceAndCustomizedPreviewHaveSeparateSessionIdsWhenBothAreAccepted()
    {
        using var f = new LearningPathV4Fixture(); var source = await f.Proposal("101");
        var adjusted = await f.Planner.CustomizePathProposalAsync(f.Center, f.Student, source.ProposalId, Options("101"));
        Assert.Empty(source.Milestones.SelectMany(m => m.Missions).Select(m => m.MissionId)
            .Intersect(adjusted.Milestones.SelectMany(m => m.Missions).Select(m => m.MissionId)));
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, source.ProposalId);
        await f.Planner.AcceptPathProposalAsync(f.Center, f.Student, adjusted.ProposalId);
        Assert.Equal(2, await f.Db.LearningPaths.CountAsync());
    }
}
