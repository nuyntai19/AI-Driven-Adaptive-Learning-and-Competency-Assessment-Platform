using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewQueue;

public sealed class TeacherApproveAIProposalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ExplicitTeacherApproval_PromotesManualProposalOnly_AndRecordsRubricHistory(bool rubric, bool deterministic)
    {
        var center = Guid.NewGuid(); var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var subject = Guid.NewGuid();
        var tenant = new TenantContext(); tenant.Initialize(center, teacher, nameof(UserRole.Teacher), 1);
        await using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, tenant);
        var now = DateTime.UtcNow;
        var question = new Question { CenterId = center, QuestionId = 1, SubjectId = subject, PrimaryTopicNodeId = 1,
            QuestionType = deterministic ? QuestionType.MultipleChoice : QuestionType.Essay,
            QuestionText = "Giải thích lựa chọn goes", CorrectAnswer = "goes", Solution = "Dùng thì hiện tại đơn.",
            MaxScore = 2, EstimatedTimeSeconds = 60, Difficulty = 1, LanguageCode = "en", Status = QuestionStatus.Active,
            CreatedAt = now, UpdatedAt = now };
        if (rubric) question.GradingCriteria.Criteria = [new() { CriterionId = "method", Title = "Phương pháp", MaxScore = 1 },
            new() { CriterionId = "answer", Title = "Kết quả", MaxScore = 1 }];
        var attempt = new Attempt { CenterId = center, AttemptId = 1, StudentId = student, QuestionId = 1, FinalAnswer = "goes",
            IsCorrect = deterministic ? true : null, AwardedScore = deterministic ? 2m : null, TimeSpentSeconds = 60,
            Confidence = 80, ReasoningLanguage = "vi", Status = AttemptStatus.NeedsTeacherReview, CreatedAt = now, UpdatedAt = now };
        var analysis = new ReasoningAnalysis { CenterId = center, AnalysisId = 1, AttemptId = 1, SchemaVersion = "ai-analysis-v1",
            SuggestedScore = deterministic ? 0m : 2m, AnswerAssessment = deterministic ? "Incorrect" : "Correct", ReasoningVerdict = "Valid",
            ReasoningQuality = 95, AnalysisConfidence = 95, Feedback = "Dùng thì hiện tại đơn nên chọn goes.",
            RootCauseNodeIds = JsonDocument.Parse("[]"), MissingSteps = JsonDocument.Parse("[]"), NeedsTeacherReview = true,
            CreatedAt = now, UpdatedAt = now };
        if (rubric) analysis.SuggestedRubricGradeJson = RubricGrade.Serialize(new() { MaxScore = 2, AwardedScore = 2,
            Criteria = [new() { CriterionId = "method", Title = "Phương pháp", MaxScore = 1, AwardedScore = 1 },
                new() { CriterionId = "answer", Title = "Kết quả", MaxScore = 1, AwardedScore = 1 }] });
        db.Questions.Add(question); db.Attempts.Add(attempt); db.ReasoningAnalyses.Add(analysis);
        db.Students.Add(new Student { CenterId = center, StudentId = student, FullName = "Synthetic Student", CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        Assert.Null(analysis.OverrideAwardedScore); // AI completion has not finalized a grade.
        var scope = new Mock<IAttemptTeacherReviewScopeGuard>();
        scope.Setup(s => s.CanAccessAttemptAsync(center, teacher, nameof(UserRole.Teacher), It.IsAny<Attempt>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new TeacherApproveUseCase(db, tenant, new EvidenceGate(), new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(db), new StudentTwinUpdater(db), new TwinUpdateHistoryWriter(db), TimeProvider.System, scopeGuard: scope.Object);
        var result = await sut.ExecuteAsync(1, new TeacherApproveRequest { OverrideVersion = 0, Note = "Giáo viên đã kiểm tra." }, default);
        Assert.Equal(TeacherApproveStatus.Success, result.Status);
        Assert.Equal(2m, result.Data!.EffectiveAwardedScore); Assert.True(result.Data.EffectiveIsCorrect);
        Assert.Equal(TeacherReviewDecision.Approved, analysis.ReviewDecision);
        Assert.Equal(teacher, analysis.ReviewedByUserId);
        Assert.Equal(deterministic ? null : 2m, analysis.OverrideAwardedScore);
        Assert.Equal(deterministic ? 2m : null, attempt.AwardedScore); // Immutable submitted/deterministic grade.
        var history = Assert.Single(await db.TeacherReviewHistories.ToListAsync());
        Assert.Equal(2m, history.NewScore);
        if (rubric) Assert.Equal(2m, RubricGrade.Deserialize(history.RubricResultJson)!.AwardedScore);
        else Assert.Null(history.RubricResultJson);
        Assert.Equal(TeacherApproveStatus.Conflict, (await sut.ExecuteAsync(1, new TeacherApproveRequest { OverrideVersion = 0 }, default)).Status);
    }
}
