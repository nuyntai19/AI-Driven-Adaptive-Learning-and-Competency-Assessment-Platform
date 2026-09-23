using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewQueue;

public sealed class VoidAssignmentQuestionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_EmptyAssignmentId_ReturnsValidationFailed()
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            Guid.Empty,
            10UL,
            new VoidAssignmentQuestionRequest { VoidReason = "Đề bài sai dữ liệu giả thiết" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroQuestionId_ReturnsValidationFailed()
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            Guid.NewGuid(),
            0UL,
            new VoidAssignmentQuestionRequest { VoidReason = "Đề bài sai dữ liệu giả thiết" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.ValidationFailed, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")] // < 5 chars
    public async Task ExecuteAsync_InvalidVoidReason_ReturnsValidationFailed(string? reason)
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            Guid.NewGuid(),
            10UL,
            new VoidAssignmentQuestionRequest { VoidReason = reason! },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_StudentRole_ReturnsForbidden()
    {
        var (context, tenant) = CreateContext(UserRole.Student);
        await using var db = context;
        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            Guid.NewGuid(),
            10UL,
            new VoidAssignmentQuestionRequest { VoidReason = "Đề bài sai dữ liệu giả thiết" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_OtherTeacherAssignment_ReturnsForbidden()
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var centerId = tenant.CenterId!.Value;
        var otherTeacherId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Classes.Add(new Class
        {
            CenterId = centerId,
            ClassId = classId,
            TeacherId = otherTeacherId, // Other teacher owns the class
            ClassName = "Other Class",
            AcademicYear = "2026-2027",
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Assignments.Add(new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            ClassId = classId,
            CreatedByTeacherId = otherTeacherId, // Other teacher created assignment
            Title = "Math Test",
            Status = AssignmentStatus.Published,
            DueAt = now.AddDays(7),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            assignmentId,
            10UL,
            new VoidAssignmentQuestionRequest { VoidReason = "Đề bài sai dữ liệu giả thiết" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_AssignmentNotFound_ReturnsNotFound()
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            Guid.NewGuid(),
            10UL,
            new VoidAssignmentQuestionRequest { VoidReason = "Đề bài sai dữ liệu giả thiết" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionNotInAssignment_ReturnsNotFound()
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var centerId = tenant.CenterId!.Value;
        var teacherId = tenant.UserId!.Value;
        var assignmentId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Classes.Add(new Class
        {
            CenterId = centerId,
            ClassId = classId,
            TeacherId = teacherId,
            ClassName = "My Class",
            AcademicYear = "2026-2027",
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Assignments.Add(new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Math Test",
            Status = AssignmentStatus.Published,
            DueAt = now.AddDays(7),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        var result = await sut.ExecuteAsync(
            assignmentId,
            999UL, // Not in assignment
            new VoidAssignmentQuestionRequest { VoidReason = "Đề bài sai dữ liệu giả thiết" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ValidTeacher_SuccessfullyVoidsQuestion_GrantsFullScore_AndQuarantinesInBank()
    {
        var (context, tenant) = CreateContext(UserRole.Teacher);
        await using var db = context;
        var centerId = tenant.CenterId!.Value;
        var teacherId = tenant.UserId!.Value;
        var studentId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var questionId = 42UL;
        var attemptId = 101UL;
        var analysisId = 202UL;
        var previousEvidenceId = 303UL;
        var now = DateTime.UtcNow;

        // Setup Teacher & Class
        db.Teachers.Add(new Teacher
        {
            CenterId = centerId,
            TeacherId = teacherId,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Classes.Add(new Class
        {
            CenterId = centerId,
            ClassId = classId,
            TeacherId = teacherId,
            ClassName = "Algebra 101",
            AcademicYear = "2026-2027",
            CreatedAt = now,
            UpdatedAt = now
        });

        // Setup Question in Bank
        db.Questions.Add(new Question
        {
            CenterId = centerId,
            QuestionId = questionId,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Tính giá trị của x trong tam giác...",
            CorrectAnswer = "A",
            Solution = "Chi tiết lời giải...",
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        // Setup Assignment & AssignmentQuestion
        db.Assignments.Add(new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Kiểm tra 15 phút Toán",
            Status = AssignmentStatus.Published,
            DueAt = now.AddDays(7),
            CreatedAt = now,
            UpdatedAt = now
        });

        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = questionId,
            OrderIndex = 1,
            Points = 10m,
            CreatedAt = now
        });

        // Setup Student Attempt with low score
        db.Attempts.Add(new Attempt
        {
            CenterId = centerId,
            AttemptId = attemptId,
            StudentId = studentId,
            AssignmentId = assignmentId,
            QuestionId = questionId,
            FinalAnswer = "B",
            IsCorrect = false,
            AwardedScore = 2.0m,
            ClientSubmissionId = Guid.NewGuid(),
            ReasoningLanguage = "vi",
            CreatedAt = now,
            UpdatedAt = now
        });

        // Setup ReasoningAnalysis
        db.ReasoningAnalyses.Add(new ReasoningAnalysis
        {
            CenterId = centerId,
            AnalysisId = analysisId,
            AttemptId = attemptId,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Học sinh chọn đáp án sai.",
            AnalysisConfidence = 95m,
            IsFallback = false,
            OverrideVersion = 0,
            CreatedAt = now,
            UpdatedAt = now
        });

        // Setup EvidenceAssessment
        db.EvidenceAssessments.Add(new EvidenceAssessment
        {
            CenterId = centerId,
            EvidenceAssessmentId = previousEvidenceId,
            AttemptId = attemptId,
            AnalysisId = analysisId,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.85m,
            ReasonCodes = JsonDocument.Parse("[\"ATTEMPT_FAIL\"]"),
            RequiresTeacherReview = false,
            PolicyVersion = "v1.0",
            AnalysisOverrideVersion = 0,
            EvaluatedAt = now,
            CreatedAt = now
        });

        // Setup StudentReviewRequest pending
        db.StudentReviewRequests.Add(new StudentReviewRequest
        {
            CenterId = centerId,
            RequestId = 1UL,
            AttemptId = attemptId,
            StudentId = studentId,
            QuestionId = questionId,
            StudentComment = "[DEFECTIVE_QUESTION_WRONG_CONTENT] Đề bài thiếu dữ kiện cạnh BC nên không thể giải được.",
            Status = StudentReviewRequestStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync();

        var sut = new VoidAssignmentQuestionUseCase(db, tenant, TimeProvider.System);

        // Execute
        var result = await sut.ExecuteAsync(
            assignmentId,
            questionId,
            new VoidAssignmentQuestionRequest
            {
                VoidReason = "Đề bài thiếu dữ kiện cạnh BC, hủy câu và cho điểm tối đa cả lớp.",
                ArchiveQuestionInBank = true
            },
            CancellationToken.None);

        // Assert UseCase Result
        Assert.True(result.IsSuccess);
        Assert.Equal(VoidAssignmentQuestionStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(assignmentId.ToString("D"), result.Data.AssignmentId);
        Assert.Equal(questionId.ToString(), result.Data.QuestionId);
        Assert.Equal(1, result.Data.VoidedAttemptsCount);
        Assert.True(result.Data.QuestionArchived);

        // Assert Attempt was updated to full score
        var updatedAttempt = await db.Attempts.SingleAsync(a => a.AttemptId == attemptId);
        Assert.True(updatedAttempt.IsCorrect);
        Assert.Equal(10.0m, updatedAttempt.AwardedScore);

        // Assert Question was archived in Question Bank
        var updatedQuestion = await db.Questions.SingleAsync(q => q.QuestionId == questionId);
        Assert.Equal(QuestionStatus.Archived, updatedQuestion.Status);

        // Assert Student Review Request was resolved
        var updatedRequest = await db.StudentReviewRequests.SingleAsync(r => r.AttemptId == attemptId);
        Assert.Equal(StudentReviewRequestStatus.Resolved, updatedRequest.Status);
        Assert.Contains("Đề bài thiếu dữ kiện", updatedRequest.TeacherNote ?? string.Empty);

        // Assert New EvidenceAssessment with zero weight was created (append-only)
        var successorEvidence = await db.EvidenceAssessments
            .SingleOrDefaultAsync(e => e.AttemptId == attemptId && e.SupersedesAssessmentId == previousEvidenceId);
        Assert.NotNull(successorEvidence);
        Assert.Equal(0m, successorEvidence.ReasoningWeight);
        Assert.Equal(EvidenceTrustLevel.ReviewOnly, successorEvidence.TrustLevel);
        Assert.Equal(EvidenceSourceType.TeacherOverride, successorEvidence.SourceType);
        Assert.Contains("QUESTION_VOIDED_BY_TEACHER", successorEvidence.ReasonCodes.RootElement.ToString());
    }

    private static (EduTwinDbContext Context, ITenantContext Tenant) CreateContext(UserRole role)
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.Initialize(centerId, userId, role.ToString(), 1);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new EduTwinDbContext(options, tenant);
        return (context, tenant);
    }
}
