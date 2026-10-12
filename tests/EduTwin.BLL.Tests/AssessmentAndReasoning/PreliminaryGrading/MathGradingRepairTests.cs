using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.PreliminaryGrading;

public sealed class MathGradingRepairTests
{
    [Fact]
    public async Task Repair_CorrectsEquivalentAnswer_AppendsEvidence_ReplaysWithoutCountingTwice()
    {
        var center = Guid.NewGuid();
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(center);
        await using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        Seed(db, center);
        await db.SaveChangesAsync();
        var repair = new MathGradingRepair(db);
        Assert.Equal(1, await repair.RepairAsync(center, 1, default));
        var attempt = await db.Attempts.SingleAsync();
        Assert.True(attempt.IsCorrect);
        Assert.Equal(20m, attempt.AwardedScore);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, attempt.Status);
        Assert.Equal(2, await db.EvidenceAssessments.CountAsync());
        Assert.Equal(1m, (await db.EvidenceAssessments.SingleAsync(e => e.EvidenceAssessmentId == 1)).ReasoningWeight);
        Assert.Equal(0m, (await db.EvidenceAssessments.OrderByDescending(e => e.EvidenceAssessmentId).FirstAsync()).ReasoningWeight);
        Assert.True((await db.ReasoningAnalyses.SingleAsync()).NeedsTeacherReview);
        Assert.Equal("Old canned response", (await db.ReasoningAnalyses.SingleAsync()).Feedback);
        Assert.Equal(1, await db.TwinUpdateHistories.CountAsync());
        Assert.Equal("math-replay-v1", (await db.TwinUpdateHistories.SingleAsync()).CalculationVersion);
        Assert.True((await db.TwinUpdateHistories.SingleAsync()).CalculationVersion.Length <= 20);
        Assert.Equal(0u, (await db.KnowledgeTwins.SingleAsync()).EvidenceCount);
        Assert.Equal(0, await repair.RepairAsync(center, 1, default));
        Assert.Equal(2, await db.EvidenceAssessments.CountAsync());
        Assert.Equal(1, await db.TwinUpdateHistories.CountAsync());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public async Task Repair_DoesNotOverwriteHumanGrade(uint overrideVersion)
    {
        var center = Guid.NewGuid();
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(center);
        await using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        Seed(db, center);
        db.ReasoningAnalyses.Local.Single().OverrideVersion = overrideVersion;
        if (overrideVersion == 0) db.ReasoningAnalyses.Local.Single().ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.Equal(0, await new MathGradingRepair(db).RepairAsync(center, 1, default));
        Assert.False((await db.Attempts.SingleAsync()).IsCorrect);
        Assert.Single(await db.EvidenceAssessments.ToListAsync());
    }

    private static void Seed(EduTwinDbContext db, Guid center)
    {
        var student = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var q = new Question { QuestionId = 1, CenterId = center, SubjectId = subject, PrimaryTopicNodeId = 1,
            CreatedByTeacherId = Guid.NewGuid(), QuestionType = QuestionType.ShortAnswer, QuestionText = "Find the domain",
            CorrectAnswer = "R\\{2}", Solution = "Denominator must be nonzero", GradingCriteria = new(),
            MaxScore = 20, Difficulty = 1, EstimatedTimeSeconds = 60, LanguageCode = "vi", AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact };
        db.Questions.Add(q);
        db.Attempts.Add(new() { AttemptId = 1, CenterId = center, StudentId = student, QuestionId = 1, Question = q,
            FinalAnswer = "D=R\\{2}", IsCorrect = false, AwardedScore = 0, ReasoningLanguage = "vi", Confidence = 80, TimeSpentSeconds = 20,
            PreliminaryGradingReasonCode = PreliminaryGradingReasonCodes.TextMismatch, Status = AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid() });
        db.ReasoningAnalyses.Add(new() { AnalysisId = 1, CenterId = center, AttemptId = 1, SchemaVersion = "ai-analysis-v1",
            ReasoningQuality = 95, AnalysisConfidence = 100, ErrorType = ErrorType.Unknown, Feedback = "Old canned response",
            MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = JsonDocument.Parse("[]"), Provider = AnalysisProvider.Gemini });
        db.EvidenceAssessments.Add(new() { EvidenceAssessmentId = 1, CenterId = center, AttemptId = 1, AnalysisId = 1,
            SourceType = EvidenceSourceType.AI, TrustLevel = EvidenceTrustLevel.Trusted, DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 1, ReasonCodes = JsonDocument.Parse("[]"), PolicyVersion = "old", EvaluatedAt = DateTime.UtcNow.AddMinutes(-1) });
        db.KnowledgeTwins.Add(new() { KnowledgeTwinId = 1, CenterId = center, StudentId = student, SubjectId = subject,
            TopicNodeId = 1, EvidenceCount = 1, MasteryPercentage = 0, LastAttemptId = 1 });
        // Match production's UTC invariant for every audited entity in this fixture.
        foreach (var entry in db.ChangeTracker.Entries())
        {
            foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(DateTime)))
                if (property.CurrentValue is DateTime dt && dt.Kind == DateTimeKind.Unspecified)
                    property.CurrentValue = DateTime.SpecifyKind(dt == default ? DateTime.UtcNow : dt, DateTimeKind.Utc);
        }
    }
}
