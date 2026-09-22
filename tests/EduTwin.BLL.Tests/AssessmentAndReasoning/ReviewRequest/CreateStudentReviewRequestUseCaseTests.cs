using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.ReviewRequest;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewRequest;

public sealed class CreateStudentReviewRequestUseCaseTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesReviewRequestAndSuccessorEvidenceWithoutUpdatingExistingEvidence()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (context, tenant) = CreateContext(centerId, studentId, nameof(UserRole.Student));

        var attempt = new Attempt
        {
            AttemptId = 100,
            CenterId = centerId,
            StudentId = studentId,
            QuestionId = 5,
            FinalAnswer = "A",
            ReasoningText = "My steps",
            IsCorrect = false,
            AwardedScore = 0,
            Status = AttemptStatus.Completed,
            ReasoningLanguage = "vi",
            CreatedAt = UtcNow.AddHours(-1),
            UpdatedAt = UtcNow.AddHours(-1)
        };

        var analysis = new ReasoningAnalysis
        {
            AnalysisId = 200,
            CenterId = centerId,
            AttemptId = 100,
            SchemaVersion = "ai-analysis-v1",
            ReasoningQuality = 60,
            ErrorType = ErrorType.Reasoning,
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Needs check",
            NeedsTeacherReview = false,
            Provider = AnalysisProvider.Gemini,
            CreatedAt = UtcNow.AddHours(-1),
            UpdatedAt = UtcNow.AddHours(-1)
        };

        var originalEvidence = new EvidenceAssessment
        {
            EvidenceAssessmentId = 300,
            CenterId = centerId,
            AttemptId = 100,
            AnalysisId = 200,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 1.0m,
            ReasonCodes = JsonDocument.Parse("[\"baseline_code\"]"),
            RequiresTeacherReview = false,
            PolicyVersion = "v1",
            EvaluatedAt = UtcNow.AddHours(-1),
            CreatedAt = UtcNow.AddHours(-1)
        };

        context.Attempts.Add(attempt);
        context.ReasoningAnalyses.Add(analysis);
        context.EvidenceAssessments.Add(originalEvidence);
        await context.SaveChangesAsync();

        var sut = new CreateStudentReviewRequestUseCase(context, tenant);
        var result = await sut.ExecuteAsync(100, new CreateStudentReviewRequest
        {
            StudentComment = "Em đã tính đúng ở bước 2, mong thầy cô xem xét lại."
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(100ul, result.Data.AttemptId);
        Assert.Equal(StudentReviewRequestStatus.Pending, result.Data.Status);
        Assert.Equal("Em đã tính đúng ở bước 2, mong thầy cô xem xét lại.", result.Data.StudentComment);

        // Attempt and Analysis updated
        var reloadedAttempt = await context.Attempts.SingleOrDefaultAsync(a => a.AttemptId == 100ul);
        Assert.NotNull(reloadedAttempt);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, reloadedAttempt.Status);

        var reloadedAnalysis = await context.ReasoningAnalyses.SingleOrDefaultAsync(ra => ra.AnalysisId == 200ul);
        Assert.NotNull(reloadedAnalysis);
        Assert.True(reloadedAnalysis.NeedsTeacherReview);

        // Original evidence remains untouched (append-only database constraint)
        var reloadedOriginalEvidence = await context.EvidenceAssessments.SingleOrDefaultAsync(e => e.EvidenceAssessmentId == 300ul);
        Assert.NotNull(reloadedOriginalEvidence);
        Assert.False(reloadedOriginalEvidence.RequiresTeacherReview);

        // Successor evidence created with RequiresTeacherReview = true
        var successorEvidence = await context.EvidenceAssessments
            .SingleOrDefaultAsync(e => e.CenterId == centerId && e.SupersedesAssessmentId == 300ul);
        Assert.NotNull(successorEvidence);
        Assert.True(successorEvidence.RequiresTeacherReview);
        Assert.Equal(100ul, successorEvidence.AttemptId);
        Assert.Equal(200ul, successorEvidence.AnalysisId);

        var codes = successorEvidence.ReasonCodes.RootElement.EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Contains(EvidenceReasonCodes.StudentReviewRequested, codes);
        Assert.Contains("baseline_code", codes);
    }

    [Fact]
    public async Task ExecuteAsync_SubmittingTwice_IsIdempotentAndDoesNotDuplicateRows()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (context, tenant) = CreateContext(centerId, studentId, nameof(UserRole.Student));

        var attempt = new Attempt
        {
            AttemptId = 101,
            CenterId = centerId,
            StudentId = studentId,
            QuestionId = 5,
            FinalAnswer = "B",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };

        var evidence = new EvidenceAssessment
        {
            EvidenceAssessmentId = 301,
            CenterId = centerId,
            AttemptId = 101,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 1.0m,
            ReasonCodes = JsonDocument.Parse("[]"),
            RequiresTeacherReview = false,
            PolicyVersion = "v1",
            EvaluatedAt = UtcNow,
            CreatedAt = UtcNow
        };

        context.Attempts.Add(attempt);
        context.EvidenceAssessments.Add(evidence);
        await context.SaveChangesAsync();

        var sut = new CreateStudentReviewRequestUseCase(context, tenant);

        var firstResult = await sut.ExecuteAsync(101, new CreateStudentReviewRequest
        {
            StudentComment = "Xem xét giúp em."
        }, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        var secondResult = await sut.ExecuteAsync(101, new CreateStudentReviewRequest
        {
            StudentComment = "Gửi lại lần nữa."
        }, CancellationToken.None);
        Assert.True(secondResult.IsSuccess);

        // Exactly one review request in DB
        var totalRequests = await context.StudentReviewRequests.CountAsync(r => r.CenterId == centerId && r.AttemptId == 101ul);
        Assert.Equal(1, totalRequests);

        // Exactly 2 evidence rows in total (1 original + 1 successor)
        var totalEvidences = await context.EvidenceAssessments.CountAsync(e => e.CenterId == centerId && e.AttemptId == 101ul);
        Assert.Equal(2, totalEvidences);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentStudentAttempt_ReturnsForbidden()
    {
        var centerId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var (context, tenant) = CreateContext(centerId, studentA, nameof(UserRole.Student));

        var attemptOfB = new Attempt
        {
            AttemptId = 102,
            CenterId = centerId,
            StudentId = studentB,
            QuestionId = 5,
            FinalAnswer = "C",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };

        context.Attempts.Add(attemptOfB);
        await context.SaveChangesAsync();

        var sut = new CreateStudentReviewRequestUseCase(context, tenant);
        var result = await sut.ExecuteAsync(102, new CreateStudentReviewRequest
        {
            StudentComment = "Xem xét bài của người khác."
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CommentTooLong_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var (context, tenant) = CreateContext(centerId, studentId, nameof(UserRole.Student));

        var sut = new CreateStudentReviewRequestUseCase(context, tenant);
        var result = await sut.ExecuteAsync(103, new CreateStudentReviewRequest
        {
            StudentComment = new string('x', 1001)
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_FAILED", result.ErrorCode);
    }

    private static (EduTwinDbContext Context, TenantContext Tenant) CreateContext(
        Guid centerId,
        Guid userId,
        string role)
    {
        var dbName = $"ReviewRequestTests_{Guid.NewGuid():N}";
        var tenant = new TenantContext();
        tenant.Initialize(centerId, userId, role, 1);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new EduTwinDbContext(options, tenant), tenant);
    }
}
